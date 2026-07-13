using System.Globalization;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.France.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.France;

/// <summary>
/// Service for reporting invoices to the French PPF (Portail Public de Facturation).
/// Generates Factur-X (CII) XML, submits via PPF API, and retrieves acknowledgments.
/// In sandbox mode, simulates PPF acceptance without making real HTTP calls.
/// </summary>
public sealed class FrancePpfService : IFrancePpfService
{
    private readonly FrancePpfConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrancePpfService> _logger;

    /// <summary>Factur-X / CII XML namespace (Cross-Industry Invoice).</summary>
    private static readonly XNamespace CiiNs = "urn:un:unece:uncefact:data:standard:CrossIndustryDocument:100";

    /// <summary>CII TypeCode for invoices.</summary>
    private const string TypeCodeInvoice = "380";

    /// <summary>Standard French VAT rate (20%).</summary>
    private const decimal DefaultVatRate = 20m;

    /// <summary>Default sandbox PPF reference prefix.</summary>
    private const string SandboxPpfPrefix = "PPF";

    /// <summary>
    /// Initializes a new instance of the <see cref="FrancePpfService"/> class.
    /// </summary>
    /// <param name="config">PPF configuration options.</param>
    /// <param name="httpClient">HTTP client for PPF API communication.</param>
    /// <param name="logger">Logger instance.</param>
    public FrancePpfService(
        IOptions<FrancePpfConfig> config,
        HttpClient httpClient,
        ILogger<FrancePpfService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string GenerateFacturXXml(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var root = new XElement(CiiNs + "CrossIndustryDocument",
            new XAttribute(XNamespace.Xmlns + "rsm", CiiNs),
            BuildHeaderExchangedDocumentContext(),
            BuildExchangedDocument(invoice),
            BuildSupplyChainTradeTransaction(invoice));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);

        return doc.Declaration + Environment.NewLine + root;
    }

    /// <inheritdoc />
    public async Task<ReportingResult> ReportAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        _logger.LogInformation(
            "Reporting invoice {InvoiceNumber} to French PPF (Sandbox={SandboxMode})",
            invoice.InvoiceNumber, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            var ppfRef = $"{SandboxPpfPrefix}{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
            _logger.LogInformation("Sandbox mode: simulated PPF acceptance with reference {PpfReference}", ppfRef);

            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = ppfRef,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = $"Sandbox acceptance for invoice {invoice.InvoiceNumber}",
            };
        }

        var facturXml = GenerateFacturXXml(invoice);
        var content = new StringContent(facturXml, System.Text.Encoding.UTF8, "application/xml");
        var response = await _httpClient.PostAsync(_config.ApiBaseUrl, content, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var ppfReference = ExtractPpfReference(responseBody);
            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = ppfReference,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody,
            };
        }

        _logger.LogWarning("PPF submission failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
        return new ReportingResult
        {
            Accepted = false,
            ErrorMessage = $"PPF returned {(int)response.StatusCode}: {responseBody}",
            Timestamp = DateTime.UtcNow,
            ProviderResponse = responseBody,
        };
    }

    /// <inheritdoc />
    public async Task<ReportingAcknowledgment> GetAcknowledgmentAsync(string ppfReference, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ppfReference);

        _logger.LogInformation("Checking PPF acknowledgment for reference {PpfReference} (Sandbox={SandboxMode})", ppfReference, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = ppfReference,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        var statusUrl = $"{_config.ApiBaseUrl.TrimEnd('/')}/status/{ppfReference}";
        var response = await _httpClient.GetAsync(statusUrl, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = ppfReference,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        return new ReportingAcknowledgment
        {
            Accepted = false,
            ReferenceId = ppfReference,
            ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ErrorMessage = responseBody,
            ReceivedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Builds the rsm:HeaderExchangedDocumentContext section.
    /// </summary>
    private XElement BuildHeaderExchangedDocumentContext()
    {
        return new XElement(CiiNs + "rsm:HeaderExchangedDocumentContext",
            new XElement(CiiNs + "ram:GuidelineSpecifiedID", "urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0"));
    }

    /// <summary>
    /// Builds the rsm:ExchangedDocument section with document metadata.
    /// </summary>
    private XElement BuildExchangedDocument(Invoice invoice)
    {
        return new XElement(CiiNs + "rsm:ExchangedDocument",
            new XElement(CiiNs + "ram:ID", invoice.InvoiceNumber),
            new XElement(CiiNs + "ram:TypeCode", TypeCodeInvoice),
            new XElement(CiiNs + "ram:IssueDateTime",
                new XElement(CiiNs + "ram:DateTimeString",
                    invoice.InvoiceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                    new XAttribute("format", "102"))));
    }

    /// <summary>
    /// Builds the rsm:SupplyChainTradeTransaction section with line items and financials.
    /// </summary>
    private XElement BuildSupplyChainTradeTransaction(Invoice invoice)
    {
        return new XElement(CiiNs + "rsm:SupplyChainTradeTransaction",
            BuildApplicableHeaderTradeAgreement(invoice),
            BuildApplicableHeaderTradeDelivery(invoice),
            BuildApplicableHeaderTradeSettlement(invoice));
    }

    /// <summary>
    /// Builds the ApplicableHeaderTradeAgreement section with seller and buyer information.
    /// </summary>
    private XElement BuildApplicableHeaderTradeAgreement(Invoice invoice)
    {
        return new XElement(CiiNs + "ram:ApplicableHeaderTradeAgreement",
            new XElement(CiiNs + "ram:SellerTradeParty",
                new XElement(CiiNs + "ram:Name", invoice.VendorName),
                new XElement(CiiNs + "ram:DefinedTradeContact",
                    new XElement(CiiNs + "ram:EmailURIUniversalCommunication",
                        new XElement(CiiNs + "ram:URIID", invoice.VendorEmail ?? string.Empty))),
                new XElement(CiiNs + "ram:PostalTradeAddress"),
                new XElement(CiiNs + "ram:TaxRegistration",
                    new XElement(CiiNs + "ram:ID", _config.VatNumber))),
            new XElement(CiiNs + "ram:BuyerTradeParty",
                new XElement(CiiNs + "ram:Name", invoice.BuyerName),
                new XElement(CiiNs + "ram:DefinedTradeContact"),
                new XElement(CiiNs + "ram:PostalTradeAddress"),
                new XElement(CiiNs + "ram:TaxRegistration",
                    new XElement(CiiNs + "ram:ID", invoice.BuyerTaxId ?? string.Empty))));
    }

    /// <summary>
    /// Builds the ApplicableHeaderTradeDelivery section.
    /// </summary>
    private static XElement BuildApplicableHeaderTradeDelivery(Invoice invoice)
    {
        return new XElement(CiiNs + "ram:ApplicableHeaderTradeDelivery",
            new XElement(CiiNs + "ram:ActualDeliverySupplyChainEvent",
                new XElement(CiiNs + "ram:OccurrenceDateTime",
                    new XElement(CiiNs + "ram:DateTimeString",
                        invoice.InvoiceDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                        new XAttribute("format", "102")))));
    }

    /// <summary>
    /// Builds the ApplicableHeaderTradeSettlement section with monetary summaries and line items.
    /// </summary>
    private XElement BuildApplicableHeaderTradeSettlement(Invoice invoice)
    {
        var lineElements = new List<XElement>();
        var lineId = 1;

        foreach (var line in invoice.Lines)
        {
            lineElements.Add(new XElement(CiiNs + "ram:ApplicableTradeProduct",
                new XElement(CiiNs + "ram:Name", line.Description),
                new XElement(CiiNs + "ram:ID", line.ProductCode ?? lineId.ToString(CultureInfo.InvariantCulture))));
            lineId++;
        }

        var vatRate = invoice.Lines.Count > 0 && invoice.Lines[0].TaxRate > 0
            ? invoice.Lines[0].TaxRate
            : DefaultVatRate;

        return new XElement(CiiNs + "ram:ApplicableHeaderTradeSettlement",
            lineElements.Count > 0 ? lineElements : [],
            new XElement(CiiNs + "ram:ApplicableTradeTax",
                new XElement(CiiNs + "ram:CalculatedAmount", FormatDecimal(invoice.TaxAmount)),
                new XElement(CiiNs + "ram:TypeCode", "VAT"),
                new XElement(CiiNs + "ram:BasisAmount", FormatDecimal(invoice.Subtotal)),
                new XElement(CiiNs + "ram:CategoryCode", "S"),
                new XElement(CiiNs + "ram:RateApplicablePercent", FormatDecimal(vatRate))),
            new XElement(CiiNs + "ram:SpecifiedLineSettlementMonetarySummary",
                new XElement(CiiNs + "ram:NetLineTotalAmount", FormatDecimal(invoice.Subtotal)),
                new XElement(CiiNs + "ram:TaxBasisTotalAmount", FormatDecimal(invoice.Subtotal)),
                new XElement(CiiNs + "ram:GrandTotalAmount", FormatDecimal(invoice.TotalAmount))),
            new XElement(CiiNs + "ram:ApplicableHeaderTradePaymentTerms",
                new XElement(CiiNs + "ram:Description", "Net 30")));
    }

    /// <summary>
    /// Extracts the PPF reference number from a response body.
    /// </summary>
    private static string? ExtractPpfReference(string responseBody)
    {
        try
        {
            var doc = XDocument.Parse(responseBody);
            return doc.Root?
                .Element(CiiNs + "ram:ID")?
                .Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>Formats a decimal value for Factur-X XML (dot as decimal separator).</summary>
    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
