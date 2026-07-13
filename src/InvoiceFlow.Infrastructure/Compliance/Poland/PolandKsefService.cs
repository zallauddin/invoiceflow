using System.Globalization;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.Poland.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Poland;

/// <summary>
/// Service for reporting invoices to the Polish KSeF (Krajowy System e-Faktur).
/// Generates FA(2) XML, submits via KSeF API with NIP authentication, and checks status.
/// In sandbox mode, simulates KSeF acceptance without making real HTTP calls.
/// </summary>
public sealed class PolandKsefService : IPolandKsefService
{
    private readonly PolandKsefConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PolandKsefService> _logger;

    /// <summary>KSeF FA(2) XML namespace (schema version 2023.12.22).</summary>
    private static readonly XNamespace KsefNs = "http://crd.gov.pl/wzor/2023/12/22/12653/";

    /// <summary>KodFormularza value for FA (faktura).</summary>
    private const string KodFormularza = "FA";

    /// <summary>WariantFormularza value (schema version 2).</summary>
    private const int WariantFormularza = 2;

    /// <summary>RodzajFaktury for a standard VAT invoice.</summary>
    private const string RodzajFakturyVat = "1";

    /// <summary>Default Polish VAT rate (23%).</summary>
    private const decimal DefaultVatRate = 23m;

    /// <summary>Default sandbox KSeF reference prefix.</summary>
    private const string SandboxKsefPrefix = "KSEF";

    /// <summary>
    /// Initializes a new instance of the <see cref="PolandKsefService"/> class.
    /// </summary>
    /// <param name="config">KSeF configuration options.</param>
    /// <param name="httpClient">HTTP client for KSeF API communication.</param>
    /// <param name="logger">Logger instance.</param>
    public PolandKsefService(
        IOptions<PolandKsefConfig> config,
        HttpClient httpClient,
        ILogger<PolandKsefService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string GenerateFaXml(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var faElement = BuildFa(invoice);
        foreach (var faWiersz in BuildFaWiersze(invoice))
        {
            faElement.Add(faWiersz);
        }

        var root = new XElement(KsefNs + "FA",
            new XAttribute(XNamespace.Xmlns + "tns", KsefNs),
            BuildNaglowek(),
            BuildPodmiot1(invoice, isBuyer: false),
            BuildPodmiot1(invoice, isBuyer: true),
            faElement);

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
            "Reporting invoice {InvoiceNumber} to KSeF (Sandbox={SandboxMode})",
            invoice.InvoiceNumber, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            var ksefRef = $"{SandboxKsefPrefix}{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
            _logger.LogInformation("Sandbox mode: simulated KSeF acceptance with reference {KsefRef}", ksefRef);

            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = ksefRef,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = $"Sandbox acceptance for invoice {invoice.InvoiceNumber}",
            };
        }

        var faXml = GenerateFaXml(invoice);
        var content = new StringContent(faXml, System.Text.Encoding.UTF8, "application/xml");

        var request = new HttpRequestMessage(HttpMethod.Post, _config.ApiBaseUrl) { Content = content };
        if (!string.IsNullOrEmpty(_config.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.Token);
        }

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            var ksefReference = ExtractKsefReference(responseBody);
            return new ReportingResult
            {
                Accepted = true,
                ReferenceId = ksefReference,
                Timestamp = DateTime.UtcNow,
                ProviderResponse = responseBody,
            };
        }

        _logger.LogWarning("KSeF submission failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
        return new ReportingResult
        {
            Accepted = false,
            ErrorMessage = $"KSeF returned {(int)response.StatusCode}: {responseBody}",
            Timestamp = DateTime.UtcNow,
            ProviderResponse = responseBody,
        };
    }

    /// <inheritdoc />
    public async Task<ReportingAcknowledgment> GetStatusAsync(string ksefReference, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ksefReference);

        _logger.LogInformation("Checking KSeF status for reference {KsefReference} (Sandbox={SandboxMode})", ksefReference, _config.SandboxMode);

        if (_config.SandboxMode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = ksefReference,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        var statusUrl = $"{_config.ApiBaseUrl.TrimEnd('/')}/api/v1/status/{ksefReference}";
        var request = new HttpRequestMessage(HttpMethod.Get, statusUrl);
        if (!string.IsNullOrEmpty(_config.Token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.Token);
        }

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ReportingAcknowledgment
            {
                Accepted = true,
                ReferenceId = ksefReference,
                ReceivedAt = DateTime.UtcNow,
            };
        }

        return new ReportingAcknowledgment
        {
            Accepted = false,
            ReferenceId = ksefReference,
            ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ErrorMessage = responseBody,
            ReceivedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Builds the Naglowek (header) element with form identification.
    /// </summary>
    private static XElement BuildNaglowek()
    {
        return new XElement(KsefNs + "Naglowek",
            new XElement(KsefNs + "KodFormularza", KodFormularza),
            new XElement(KsefNs + "WariantFormularza", WariantFormularza),
            new XElement(KsefNs + "DataWytworzeniaFa",
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Builds Podmiot1 (seller) or Podmiot2 (buyer) based on the isBuyer flag.
    /// </summary>
    private static XElement BuildPodmiot1(Invoice invoice, bool isBuyer)
    {
        var elementName = isBuyer ? "Podmiot2" : "Podmiot1";
        var name = isBuyer ? invoice.BuyerName : invoice.VendorName;
        var taxId = isBuyer ? invoice.BuyerTaxId : invoice.VendorTaxId;

        var podmiot = new XElement(KsefNs + elementName,
            new XElement(KsefNs + "NIP", taxId ?? string.Empty),
            new XElement(KsefNs + "Nazwa", name));

        if (!isBuyer)
        {
            podmiot.Add(new XElement(KsefNs + "Adres",
                new XElement(KsefNs + "KodKraju", "PL"),
                new XElement(KsefNs + "Ulica", "ul. Przykładowa"),
                new XElement(KsefNs + "NrDomu", "1"),
                new XElement(KsefNs + "KodPocztowy", "00-001"),
                new XElement(KsefNs + "Miejscowosc", "Warszawa")));
        }

        return podmiot;
    }

    /// <summary>
    /// Builds the Fa (invoice summary) element.
    /// </summary>
    private XElement BuildFa(Invoice invoice)
    {
        var netAmount = invoice.Subtotal;
        var vatAmount = invoice.TaxAmount;
        var grossAmount = invoice.TotalAmount;

        var fa = new XElement(KsefNs + "Fa",
            new XElement(KsefNs + "NrFaktury", invoice.InvoiceNumber),
            new XElement(KsefNs + "DataWystawienia",
                invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(KsefNs + "RodzajFaktury", RodzajFakturyVat),
            new XElement(KsefNs + "P_1", "M"),
            new XElement(KsefNs + "P_13_1", FormatDecimal(netAmount)),
            new XElement(KsefNs + "P_13_6", FormatDecimal(grossAmount)),
            new XElement(KsefNs + "P_14_1", FormatDecimal(vatAmount)),
            new XElement(KsefNs + "P_14_2", FormatDecimal(vatAmount)));

        return fa;
    }

    /// <summary>
    /// Builds FaWiersz (line item) elements for each invoice line.
    /// </summary>
    private static IEnumerable<XElement> BuildFaWiersze(Invoice invoice)
    {
        var lineCounter = 1;
        foreach (var line in invoice.Lines)
        {
            yield return new XElement(KsefNs + "FaWiersz",
                new XElement(KsefNs + "NrWiersza", lineCounter++),
                new XElement(KsefNs + "P_7", line.Description),
                new XElement(KsefNs + "P_8A", FormatDecimal(line.Quantity)),
                new XElement(KsefNs + "P_9A", FormatDecimal(line.UnitPrice)),
                new XElement(KsefNs + "P_11", FormatDecimal(line.LineTotal)),
                new XElement(KsefNs + "P_12", FormatDecimal(line.TaxRate)));
        }
    }

    /// <summary>
    /// Extracts the KSeF reference number from a response body.
    /// </summary>
    private static string? ExtractKsefReference(string responseBody)
    {
        try
        {
            var doc = XDocument.Parse(responseBody);
            return doc.Root?
                .Element(KsefNs + "NumerFa")?
                .Value
                ?? doc.Root?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>Formats a decimal value for KSeF XML (dot as decimal separator).</summary>
    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
}
