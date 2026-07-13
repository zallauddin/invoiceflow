using System.Globalization;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.Italy.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Italy;

/// <summary>
/// Service for transmitting invoices to the Italian SdI (Sistema di Interscambio).
/// Generates FatturaPA XML, transmits via SdI web service, and checks processing status.
/// In sandbox mode, simulates SdI acceptance without making real HTTP calls.
/// </summary>
public sealed class ItalySdiService : IItalySdiService
{
    private readonly ItalySdiConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ItalySdiService> _logger;

    /// <summary>FatturaPA XML namespace.</summary>
    private static readonly XNamespace FatturaPaNs = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    /// <summary>Default CodiceDestinatario for PEC delivery.</summary>
    private const string DefaultCodiceDestinatario = "0000000";

    /// <summary>Standard TipoDocumento for invoices.</summary>
    private const string TipoDocumentoFattura = "TD01";

    /// <summary>Default SdI prefix for sandbox identifiers.</summary>
    private const string SandboxSdiPrefix = "SAND";

    /// <summary>
    /// Initializes a new instance of the <see cref="ItalySdiService"/> class.
    /// </summary>
    /// <param name="config">SdI configuration options.</param>
    /// <param name="httpClient">HTTP client for SdI API communication.</param>
    /// <param name="logger">Logger instance.</param>
    public ItalySdiService(
        IOptions<ItalySdiConfig> config,
        HttpClient httpClient,
        ILogger<ItalySdiService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string FormatFatturaPaForSdi(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var root = new XElement(FatturaPaNs + "FatturaElettronica",
            new XAttribute(XNamespace.Xmlns + "p", FatturaPaNs),
            new XAttribute("versione", "FPA12"),
            BuildHeader(invoice),
            BuildBody(invoice));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);

        return doc.Declaration + Environment.NewLine + root;
    }

    /// <inheritdoc />
    public async Task<ReportingResult> TransmitAsync(Invoice invoice, string fatturaPaXml, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(fatturaPaXml);

        _logger.LogInformation(
            "Transmitting invoice {InvoiceNumber} to SdI (Sandbox={SandboxMode})",
            invoice.InvoiceNumber, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            var sdiId = $"{SandboxSdiPrefix}{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
            _logger.LogInformation("Sandbox mode: simulated SdI acceptance with identifier {SdiId}", sdiId);

            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = sdiId,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = $"Sandbox acceptance for invoice {invoice.InvoiceNumber}",
            };
        }

        var content = new StringContent(fatturaPaXml, System.Text.Encoding.UTF8, "application/xml");
        var response = await _httpClient.PostAsync(_config.ApiBaseUrl, content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var sdiIdentifier = ExtractSdiIdentifier(responseBody);
            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = sdiIdentifier,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody,
            };
        }

        _logger.LogWarning("SdI transmission failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
        return new ReportingResult
        {
            Accepted = false,
            ErrorMessage = $"SdI returned {(int)response.StatusCode}: {responseBody}",
            Timestamp = DateTime.UtcNow,
            ProviderResponse = responseBody,
        };
    }

    /// <inheritdoc />
    public async Task<ReportingAcknowledgment> CheckStatusAsync(string sdiIdentifier, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sdiIdentifier);

        _logger.LogInformation("Checking SdI status for identifier {SdiIdentifier} (Sandbox={SandboxMode})", sdiIdentifier, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = sdiIdentifier,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        var statusUrl = $"{_config.ApiBaseUrl.TrimEnd('/')}/status/{sdiIdentifier}";
        var response = await _httpClient.GetAsync(statusUrl, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = sdiIdentifier,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        return new ReportingAcknowledgment
        {
            Accepted = false,
            ReferenceId = sdiIdentifier,
            ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ErrorMessage = responseBody,
            ReceivedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Builds the FatturaPA header section (DatiTrasmissione, CedentePrestatore, CessionarioCommittente).
    /// </summary>
    private XElement BuildHeader(Invoice invoice)
    {
        return new XElement(FatturaPaNs + "FatturaElettronicaHeader",
            new XElement(FatturaPaNs + "DatiTrasmissione",
                new XElement(FatturaPaNs + "IdTrasmittente",
                    new XElement(FatturaPaNs + "IdPaese", "IT"),
                    new XElement(FatturaPaNs + "IdCodice", _config.CodiceFiscale)),
                new XElement(FatturaPaNs + "ProgressivoInvio", invoice.InvoiceNumber),
                new XElement(FatturaPaNs + "FormatoTrasmissione", "FPA12"),
                new XElement(FatturaPaNs + "CodiceDestinatario", DefaultCodiceDestinatario),
                new XElement(FatturaPaNs + "ContattoTrasmittente",
                    new XElement(FatturaPaNs + "PECLocalbo", _config.PecAddress))),
            BuildCedentePrestatore(invoice),
            BuildCessionarioCommittente(invoice));
    }

    /// <summary>
    /// Builds the CedentePrestatore (seller/supplier) section.
    /// </summary>
    private XElement BuildCedentePrestatore(Invoice invoice)
    {
        return new XElement(FatturaPaNs + "CedentePrestatore",
            new XElement(FatturaPaNs + "DatiAnagrafici",
                new XElement(FatturaPaNs + "IdFiscaleIVA",
                    new XElement(FatturaPaNs + "IdPaese", "IT"),
                    new XElement(FatturaPaNs + "IdCodice", _config.PartitaIva)),
                new XElement(FatturaPaNs + "Anagrafica",
                    new XElement(FatturaPaNs + "Denominazione", invoice.VendorName)),
                new XElement(FatturaPaNs + "RegimeFiscale", "RF01")),
            new XElement(FatturaPaNs + "Sede",
                new XElement(FatturaPaNs + "Indirizzo", "Via non specificata"),
                new XElement(FatturaPaNs + "CAP", "00100"),
                new XElement(FatturaPaNs + "Comune", "Roma"),
                new XElement(FatturaPaNs + "Nazione", "IT")));
    }

    /// <summary>
    /// Builds the CessionarioCommittente (buyer/customer) section.
    /// </summary>
    private XElement BuildCessionarioCommittente(Invoice invoice)
    {
        var datiAnagrafici = new XElement(FatturaPaNs + "DatiAnagrafici",
            new XElement(FatturaPaNs + "Anagrafica",
                new XElement(FatturaPaNs + "Denominazione", invoice.BuyerName)));

        if (!string.IsNullOrEmpty(invoice.BuyerTaxId))
        {
            datiAnagrafici.Add(new XElement(FatturaPaNs + "IdFiscaleIVA",
                new XElement(FatturaPaNs + "IdPaese", "IT"),
                new XElement(FatturaPaNs + "IdCodice", invoice.BuyerTaxId)));
        }

        return new XElement(FatturaPaNs + "CessionarioCommittente",
            datiAnagrafici,
            new XElement(FatturaPaNs + "Sede",
                new XElement(FatturaPaNs + "Indirizzo", "Via non specificata"),
                new XElement(FatturaPaNs + "CAP", "00100"),
                new XElement(FatturaPaNs + "Comune", "Roma"),
                new XElement(FatturaPaNs + "Nazione", "IT")));
    }

    /// <summary>
    /// Builds the FatturaPA body section (DatiGenerali, DatiBeniServizi, DatiPagamento).
    /// </summary>
    private XElement BuildBody(Invoice invoice)
    {
        var body = new XElement(FatturaPaNs + "FatturaElettronicaBody",
            new XElement(FatturaPaNs + "DatiGenerali",
                new XElement(FatturaPaNs + "DatiGeneraliDocumento",
                    new XElement(FatturaPaNs + "TipoDocumento", TipoDocumentoFattura),
                    new XElement(FatturaPaNs + "Divisa", invoice.Currency ?? "EUR"),
                    new XElement(FatturaPaNs + "Data", invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XElement(FatturaPaNs + "Numero", invoice.InvoiceNumber),
                    new XElement(FatturaPaNs + "ImportoTotaleDocumento", FormatDecimal(invoice.TotalAmount)))),
            BuildDatiBeniServizi(invoice),
            new XElement(FatturaPaNs + "DatiPagamento",
                new XElement(FatturaPaNs + "CondizioniPagamento", "TP02"),
                new XElement(FatturaPaNs + "DettaglioPagamento",
                    new XElement(FatturaPaNs + "ModalitaPagamento", "MP05"),
                    new XElement(FatturaPaNs + "ImportoPagato", FormatDecimal(invoice.TotalAmount)),
                    new XElement(FatturaPaNs + "DataScadenzaPagamento",
                        (invoice.DueDate ?? invoice.InvoiceDate.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))));

        return body;
    }

    /// <summary>
    /// Builds the DatiBeniServizi section with line items and tax summaries.
    /// </summary>
    private XElement BuildDatiBeniServizi(Invoice invoice)
    {
        var lineElements = new List<XElement>();
        var riepilogoMap = new Dictionary<decimal, (decimal Imponibile, decimal Imposta)>(invoice.Lines.Count);

        var lineNumber = 1;
        foreach (var line in invoice.Lines)
        {
            lineElements.Add(new XElement(FatturaPaNs + "DettaglioLinee",
                new XElement(FatturaPaNs + "NumeroLinea", lineNumber++),
                new XElement(FatturaPaNs + "Descrizione", line.Description),
                new XElement(FatturaPaNs + "Quantita", FormatDecimal(line.Quantity)),
                new XElement(FatturaPaNs + "PrezzoUnitario", FormatDecimal(line.UnitPrice)),
                new XElement(FatturaPaNs + "PrezzoTotale", FormatDecimal(line.LineTotal)),
                new XElement(FatturaPaNs + "AliquotaIVA", FormatDecimal(line.TaxRate))));

            if (!riepilogoMap.TryGetValue(line.TaxRate, out var riepilogo))
            {
                riepilogo = (0m, 0m);
            }

            riepilogoMap[line.TaxRate] = (riepilogo.Imponibile + line.LineTotal, riepilogo.Imposta + line.TaxAmount);
        }

        var riepilogoElements = riepilogoMap.Select(kvp =>
            new XElement(FatturaPaNs + "DatiRiepilogo",
                new XElement(FatturaPaNs + "AliquotaIVA", FormatDecimal(kvp.Key)),
                new XElement(FatturaPaNs + "ImponibileImporto", FormatDecimal(kvp.Value.Imponibile)),
                new XElement(FatturaPaNs + "Imposta", FormatDecimal(kvp.Value.Imposta)),
                new XElement(FatturaPaNs + "EsigibilitaIVA", "I")));

        return new XElement(FatturaPaNs + "DatiBeniServizi",
            lineElements.Concat(riepilogoElements));
    }

    /// <summary>
    /// Extracts the SdI identifier from an XML response body.
    /// </summary>
    private static string? ExtractSdiIdentifier(string responseBody)
    {
        try
        {
            var doc = XDocument.Parse(responseBody);
            var identifier = doc.Root?
                .Element(FatturaPaNs + "IdentificativoSdI")?
                .Value;
            return identifier;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>Formats a decimal value for FatturaPA XML (comma as decimal separator).</summary>
    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
}
