using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.Brazil.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Brazil;

/// <summary>
/// Brazilian NF-e compliance service — generates NF-e XML, computes digests, and submits to SEFAZ.
/// </summary>
public sealed class BrazilNfeService : IBrazilNfeService
{
    private readonly HttpClient _httpClient;
    private readonly BrazilSefazConfig _config;
    private readonly ILogger<BrazilNfeService> _logger;

    /// <summary> Initializes a new instance of the <see cref="BrazilNfeService"/> class.</summary>
    public BrazilNfeService(
        HttpClient httpClient,
        IOptions<BrazilSefazConfig> config,
        ILogger<BrazilNfeService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string GenerateNfeXml(Invoice invoice)
    {
        var nfe = BuildNfeModel(invoice);
        var doc = BuildXDocument(nfe);
        return doc.Declaration is null
            ? doc.ToString()
            : $"{doc.Declaration}\n{doc}";
    }

    /// <inheritdoc/>
    public string ComputeNfeDigest(string xmlContent)
    {
        var bytes = Encoding.UTF8.GetBytes(xmlContent);
        var hash = SHA1.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <inheritdoc/>
    public async Task<ClearanceResult> SubmitNfeAsync(Invoice invoice, CancellationToken ct = default)
    {
        _logger.LogInformation("Submitting NF-e for invoice {InvoiceNumber}", invoice.InvoiceNumber);

        if (_config.SandboxMode)
        {
            _logger.LogInformation("Sandbox mode — simulating NF-e clearance for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = true,
                ClearanceId = $"NFE-SANDBOX-{invoice.InvoiceNumber}",
                Timestamp = DateTime.UtcNow,
                ProviderResponse = $"{{\"status\":\"approved\",\"ambiente\":\"homologacao\",\"nNF\":\"{invoice.InvoiceNumber}\"}}"
            };
        }

        var xml = GenerateNfeXml(invoice);
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");

        var endpoint = $"{_config.ApiBaseUrl.TrimEnd('/')}/NFeAutorizacao4";

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var digest = ComputeNfeDigest(xml);
                return new ClearanceResult
                {
                    Cleared = true,
                    ClearanceId = digest,
                    Timestamp = DateTime.UtcNow,
                    ProviderResponse = responseBody
                };
            }

            _logger.LogWarning("SEFAZ returned status {StatusCode} for invoice {InvoiceNumber}", response.StatusCode, invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"SEFAZ returned status {response.StatusCode}",
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error submitting NF-e for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"HTTP error communicating with SEFAZ: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Builds a <see cref="BrazilNfeXml"/> model from the core <see cref="Invoice"/> entity.
    /// </summary>
    private static BrazilNfeXml BuildNfeModel(Invoice invoice)
    {
        var items = invoice.Lines.Select(line =>
        {
            var icmsValue = Math.Round(line.LineTotal * BrazilNfeConstants.IcmsStandardRate / 100m, 2);
            var pisValue = Math.Round(line.LineTotal * BrazilNfeConstants.PisRate / 100m, 2);
            var cofinsValue = Math.Round(line.LineTotal * BrazilNfeConstants.CofinsRate / 100m, 2);

            return new BrazilNfeItem
            {
                Codigo = line.ProductCode ?? line.LineNumber.ToString("D3"),
                Descricao = line.Description,
                NcmCode = line.HsnCode ?? "00000000",
                Cfop = "5102",
                Unidade = line.Unit ?? "UN",
                Quantidade = line.Quantity,
                ValorUnitario = line.UnitPrice,
                ValorTotal = line.LineTotal,
                IcmsRate = BrazilNfeConstants.IcmsStandardRate,
                IcmsValue = icmsValue,
                PisRate = BrazilNfeConstants.PisRate,
                PisValue = pisValue,
                CofinsRate = BrazilNfeConstants.CofinsRate,
                CofinsValue = cofinsValue
            };
        }).ToList();

        var totalIcms = items.Sum(i => i.IcmsValue);
        var totalPis = items.Sum(i => i.PisValue);
        var totalCofins = items.Sum(i => i.CofinsValue);

        return new BrazilNfeXml
        {
            NfeNumber = invoice.InvoiceNumber,
            NfeDate = invoice.InvoiceDate,
            CnpjEmitente = invoice.VendorTaxId ?? string.Empty,
            CnpjDestinatario = invoice.BuyerTaxId ?? string.Empty,
            StateRegistration = string.Empty,
            TotalNfe = invoice.TotalAmount,
            IcmsBase = invoice.Subtotal,
            IcmsValue = totalIcms,
            PisValue = totalPis,
            CofinsValue = totalCofins,
            Items = items
        };
    }

    /// <summary>
    /// Constructs the XDocument representing the NF-e XML per version 4.00 schema.
    /// </summary>
    private static XDocument BuildXDocument(BrazilNfeXml nfe)
    {
        var ns = BrazilNfeConstants.NfeNamespace;
        var tpAmb = nfe.NfeDate.Year > 1 ? BrazilNfeConstants.Producao : BrazilNfeConstants.Homologacao;

        var items = nfe.Items.Select((item, index) =>
            new XElement(XName.Get("det", ns),
                new XAttribute("nItem", (index + 1).ToString(CultureInfo.InvariantCulture)),
                new XElement(XName.Get("prod", ns),
                    new XElement(XName.Get("cProd", ns), item.Codigo),
                    new XElement(XName.Get("xProd", ns), item.Descricao),
                    new XElement(XName.Get("NCM", ns), item.NcmCode),
                    new XElement(XName.Get("CFOP", ns), item.Cfop),
                    new XElement(XName.Get("uCom", ns), item.Unidade),
                    new XElement(XName.Get("qCom", ns), item.Quantidade.ToString("F4", CultureInfo.InvariantCulture)),
                    new XElement(XName.Get("vUnCom", ns), item.ValorUnitario.ToString("F10", CultureInfo.InvariantCulture)),
                    new XElement(XName.Get("vProd", ns), item.ValorTotal.ToString("F2", CultureInfo.InvariantCulture))
                ),
                new XElement(XName.Get("imposto", ns),
                    new XElement(XName.Get("ICMS", ns),
                        new XElement(XName.Get("ICMS00", ns),
                            new XElement(XName.Get("orig", ns), "0"),
                            new XElement(XName.Get("CST", ns), "00"),
                            new XElement(XName.Get("vBC", ns), item.ValorTotal.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("pICMS", ns), item.IcmsRate.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vICMS", ns), item.IcmsValue.ToString("F2", CultureInfo.InvariantCulture))
                        )
                    ),
                    new XElement(XName.Get("PIS", ns),
                        new XElement(XName.Get("PIS01", ns),
                            new XElement(XName.Get("CST", ns), "01"),
                            new XElement(XName.Get("vBC", ns), item.ValorTotal.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("pPIS", ns), item.PisRate.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vPIS", ns), item.PisValue.ToString("F2", CultureInfo.InvariantCulture))
                        )
                    ),
                    new XElement(XName.Get("COFINS", ns),
                        new XElement(XName.Get("COFINS01", ns),
                            new XElement(XName.Get("CST", ns), "01"),
                            new XElement(XName.Get("vBC", ns), item.ValorTotal.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("pCOFINS", ns), item.CofinsRate.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vCOFINS", ns), item.CofinsValue.ToString("F2", CultureInfo.InvariantCulture))
                        )
                    )
                )
            )
        );

        var doc = new XDocument(
            new XDeclaration("1.0", BrazilNfeConstants.Encoding, null),
            new XElement(XName.Get("NFe", ns),
                new XElement(XName.Get("infNFe", ns),
                    new XAttribute("versao", BrazilNfeConstants.NfeVersion),
                    new XElement(XName.Get("ide", ns),
                        new XElement(XName.Get("nNF", ns), nfe.NfeNumber),
                        new XElement(XName.Get("dhEmi", ns), nfe.NfeDate.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
                        new XElement(XName.Get("tpNF", ns), BrazilNfeConstants.TipoSaida),
                        new XElement(XName.Get("tpAmb", ns), tpAmb)
                    ),
                    new XElement(XName.Get("emit", ns),
                        new XElement(XName.Get("CNPJ", ns), nfe.CnpjEmitente),
                        new XElement(XName.Get("IE", ns), nfe.StateRegistration)
                    ),
                    new XElement(XName.Get("dest", ns),
                        new XElement(XName.Get("CNPJ", ns), nfe.CnpjDestinatario)
                    ),
                    items,
                    new XElement(XName.Get("total", ns),
                        new XElement(XName.Get("ICMSTot", ns),
                            new XElement(XName.Get("vBC", ns), nfe.IcmsBase.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vICMS", ns), nfe.IcmsValue.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vProd", ns), nfe.TotalNfe.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vNF", ns), nfe.TotalNfe.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vPIS", ns), nfe.PisValue.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(XName.Get("vCOFINS", ns), nfe.CofinsValue.ToString("F2", CultureInfo.InvariantCulture))
                        )
                    )
                )
            )
        );

        return doc;
    }
}
