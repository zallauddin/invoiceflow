using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.Mexico.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Mexico;

/// <summary>
/// Mexican CFDI compliance service — generates CFDI 4.0 XML, computes digests, and submits to a PAC.
/// </summary>
public sealed class MexicoCfdiService : IMexicoCfdiService
{
    private readonly HttpClient _httpClient;
    private readonly MexicoPacConfig _config;
    private readonly ILogger<MexicoCfdiService> _logger;

    /// <summary>SAT CFDI 4.0 namespace prefix and URIs.</summary>
    private static class CfdiNamespaces
    {
        /// <summary>CFDI core namespace.</summary>
        public const string Cfdi = "http://www.sat.gob.mx/cfd/4";

        /// <summary>CFDI prefix for root elements.</summary>
        public const string CfdiPrefix = "cfdi";

        /// <summary>CFDI schema location.</summary>
        public const string CfdiSchemaLocation =
            "http://www.sat.gob.mx/cfd/4 http://www.sat.gob.mx/sitio_internet/cfd/4/cfdv40.xsd";
    }

    /// <summary>Initializes a new instance of the <see cref="MexicoCfdiService"/> class.</summary>
    public MexicoCfdiService(
        HttpClient httpClient,
        IOptions<MexicoPacConfig> config,
        ILogger<MexicoCfdiService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string GenerateCfdiXml(Invoice invoice)
    {
        var cfdi = BuildCfdiModel(invoice);
        var doc = BuildXDocument(cfdi);
        return doc.Declaration is null
            ? doc.ToString()
            : $"{doc.Declaration}\n{doc}";
    }

    /// <inheritdoc/>
    public string ComputeCfdiDigest(string xmlContent)
    {
        var bytes = Encoding.UTF8.GetBytes(xmlContent);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <inheritdoc/>
    public async Task<ClearanceResult> StampCfdiAsync(Invoice invoice, CancellationToken ct = default)
    {
        _logger.LogInformation("Submitting CFDI for stamping — invoice {InvoiceNumber}", invoice.InvoiceNumber);

        if (_config.SandboxMode)
        {
            _logger.LogInformation("Sandbox mode — simulating CFDI stamping for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            var uuid = Guid.NewGuid().ToString("D").ToUpperInvariant();
            return new ClearanceResult
            {
                Cleared = true,
                ClearanceId = uuid,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = $"{{\"status\":\"stamped\",\"uuid\":\"{uuid}\",\"provider\":\"sandbox\",\"rfc\":\"{_config.Rfc}\"}}"
            };
        }

        var xml = GenerateCfdiXml(invoice);
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");

        var endpoint = $"{_config.PacApiBaseUrl.TrimEnd('/')}/cfdi/stamp";

        try
        {
            var response = await _httpClient.PostAsync(endpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var digest = ComputeCfdiDigest(xml);
                return new ClearanceResult
                {
                    Cleared = true,
                    ClearanceId = digest,
                    Timestamp = DateTime.UtcNow,
                    ProviderResponse = responseBody
                };
            }

            _logger.LogWarning("PAC returned status {StatusCode} for invoice {InvoiceNumber}", response.StatusCode, invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"PAC returned status {response.StatusCode}",
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error submitting CFDI for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            return new ClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"HTTP error communicating with PAC: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Builds a <see cref="MexicoCfdiXml"/> model from the core <see cref="Invoice"/> entity.
    /// </summary>
    private static MexicoCfdiXml BuildCfdiModel(Invoice invoice)
    {
        const decimal ivaRate = 16.0m;

        var conceptos = invoice.Lines.Select(line =>
        {
            var baseIva = line.LineTotal;
            var ivaAmount = Math.Round(baseIva * ivaRate / 100m, 2);

            return new MexicoCfdiConcepto
            {
                ClaveProdServ = line.ProductCode ?? "01010101",
                Cantidad = line.Quantity,
                ClaveUnidad = "H87",
                Descripcion = line.Description,
                ValorUnitario = line.UnitPrice,
                Importe = line.LineTotal,
                ObjetoImp = "02",
                TrasladoIva16 = ivaAmount,
                BaseIva16 = baseIva
            };
        }).ToList();

        var totalIva = conceptos.Sum(c => c.TrasladoIva16);

        return new MexicoCfdiXml
        {
            Uuid = string.Empty,
            Fecha = invoice.InvoiceDate,
            RfcEmisor = invoice.VendorTaxId ?? string.Empty,
            NombreEmisor = invoice.VendorName,
            RegimenFiscal = "601",
            RfcReceptor = invoice.BuyerTaxId ?? string.Empty,
            NombreReceptor = invoice.BuyerName,
            UsoCfdi = "G03",
            SubTotal = invoice.Subtotal,
            Total = invoice.Subtotal + totalIva,
            ImpuestoTrasladoIva16 = totalIva,
            Moneda = invoice.Currency,
            TipoCfdi = "I",
            MetodoPago = "PUE",
            FormaPago = "03",
            Conceptos = conceptos
        };
    }

    /// <summary>
    /// Constructs the XDocument representing the CFDI 4.0 XML per SAT specification.
    /// </summary>
    private static XDocument BuildXDocument(MexicoCfdiXml cfdi)
    {
        var cfdiNs = XNamespace.Get(CfdiNamespaces.Cfdi);

        var conceptos = cfdi.Conceptos.Select(c =>
            new XElement(cfdiNs + "Concepto",
                new XAttribute("ClaveProdServ", c.ClaveProdServ),
                new XAttribute("Cantidad", c.Cantidad.ToString("F6", CultureInfo.InvariantCulture)),
                new XAttribute("ClaveUnidad", c.ClaveUnidad),
                new XAttribute("Descripcion", c.Descripcion),
                new XAttribute("ValorUnitario", c.ValorUnitario.ToString("F6", CultureInfo.InvariantCulture)),
                new XAttribute("Importe", c.Importe.ToString("F6", CultureInfo.InvariantCulture)),
                new XAttribute("ObjetoImp", c.ObjetoImp)
            )
        );

        var traslados = cfdi.Conceptos
            .Where(c => c.TrasladoIva16 > 0)
            .Select(c =>
                new XElement(cfdiNs + "Traslado",
                    new XAttribute("Base", c.BaseIva16.ToString("F2", CultureInfo.InvariantCulture)),
                    new XAttribute("Impuesto", "002"),
                    new XAttribute("TipoFactor", "Tasa"),
                    new XAttribute("TasaOCuota", "0.160000"),
                    new XAttribute("Importe", c.TrasladoIva16.ToString("F2", CultureInfo.InvariantCulture))
                )
            );

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(cfdiNs + "Comprobante",
                new XAttribute("Version", "4.0"),
                new XAttribute("Serie", string.Empty),
                new XAttribute("Folio", string.Empty),
                new XAttribute("Fecha", cfdi.Fecha.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
                new XAttribute("FormaPago", cfdi.FormaPago),
                new XAttribute("NoCertificado", string.Empty),
                new XAttribute("Certificado", string.Empty),
                new XAttribute("SubTotal", cfdi.SubTotal.ToString("F2", CultureInfo.InvariantCulture)),
                new XAttribute("Moneda", cfdi.Moneda),
                new XAttribute("Total", cfdi.Total.ToString("F2", CultureInfo.InvariantCulture)),
                new XAttribute("TipoDeComprobante", cfdi.TipoCfdi),
                new XAttribute("MetodoPago", cfdi.MetodoPago),
                new XAttribute("LugarExpedicion", string.Empty),
                new XAttribute("Exportacion", "01"),
                new XAttribute("Sello", string.Empty),

                new XElement(cfdiNs + "Emisor",
                    new XAttribute("Rfc", cfdi.RfcEmisor),
                    new XAttribute("Nombre", cfdi.NombreEmisor),
                    new XAttribute("RegimenFiscal", cfdi.RegimenFiscal)
                ),

                new XElement(cfdiNs + "Receptor",
                    new XAttribute("Rfc", cfdi.RfcReceptor),
                    new XAttribute("Nombre", cfdi.NombreReceptor),
                    new XAttribute("RegimenFiscalReceptor", "601"),
                    new XAttribute("DomicilioFiscalReceptor", string.Empty),
                    new XAttribute("UsoCFDI", cfdi.UsoCfdi)
                ),

                new XElement(cfdiNs + "Conceptos", conceptos),

                new XElement(cfdiNs + "Impuestos",
                    new XAttribute("TotalImpuestosTrasladados", cfdi.ImpuestoTrasladoIva16.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(cfdiNs + "Traslados", traslados)
                ),

                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute("SchemaLocation", CfdiNamespaces.CfdiSchemaLocation)
            )
        );

        return doc;
    }
}
