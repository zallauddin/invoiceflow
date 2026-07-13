using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Compliance.Zatca.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Zatca;

/// <summary>
/// ZATCA (Saudi Arabia) e-invoicing compliance service.
/// Generates FATOORAH XML, TLV QR codes, computes invoice hashes, and requests clearance via the ZATCA API.
/// </summary>
public sealed class ZatcaComplianceService : IZatcaComplianceService
{
    private static readonly XNamespace XmlNs = "urn:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace CacNs = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace CbcNs = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace ExtNs = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace SaiNs = "urn:au:schema:sai:io:extensions:crossBorder:1.0";
    private static readonly XNamespace QrcNs = "urn:qr:schema:v2";

    private const decimal SaudiVatRate = 15.0m;
    private const string SaVatCategoryCode = "S";

    private readonly ZatcaApiConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZatcaComplianceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZatcaComplianceService"/> class.
    /// </summary>
    /// <param name="config">ZATCA API configuration options.</param>
    /// <param name="httpClientFactory">Factory for creating ZATCA API HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public ZatcaComplianceService(
        IOptions<ZatcaApiConfig> config,
        IHttpClientFactory httpClientFactory,
        ILogger<ZatcaComplianceService> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string GenerateFatoorahXml(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var vatAmount = invoice.TaxAmount;
        var subtotal = invoice.Subtotal;
        var totalWithVat = invoice.TotalAmount;

        var lines = invoice.Lines.Select(line => new ZatcaInvoiceLine
        {
            Id = line.LineNumber,
            Description = line.Description,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            LineAmount = line.LineTotal,
            VatAmount = line.TaxAmount,
            VatRate = line.TaxRate > 0 ? line.TaxRate : SaudiVatRate
        }).ToList();

        var xmlModel = new ZatcaInvoiceXml
        {
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            SupplierName = invoice.VendorName,
            SupplierVatNumber = invoice.VendorTaxId ?? string.Empty,
            CustomerName = invoice.BuyerName,
            CustomerVatNumber = invoice.BuyerTaxId,
            TotalAmount = totalWithVat,
            SubtotalAmount = subtotal,
            TaxAmount = vatAmount,
            VatAmount = vatAmount,
            Currency = invoice.Currency,
            Lines = lines
        };

        return BuildFatoorahXml(xmlModel);
    }

    /// <inheritdoc />
    public string GenerateTlvQrCode(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var sellerName = invoice.VendorName;
        var vatNumber = invoice.VendorTaxId ?? string.Empty;
        var timestamp = invoice.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var totalWithVat = invoice.TotalAmount.ToString("F2", CultureInfo.InvariantCulture);
        var vatTotal = invoice.TaxAmount.ToString("F2", CultureInfo.InvariantCulture);

        var tlvBytes = new List<byte>();
        tlvBytes.AddRange(BuildTlvEntry(ZatcaTlvTag.SellerName, Encoding.UTF8.GetBytes(sellerName)));
        tlvBytes.AddRange(BuildTlvEntry(ZatcaTlvTag.VatRegistrationNumber, Encoding.UTF8.GetBytes(vatNumber)));
        tlvBytes.AddRange(BuildTlvEntry(ZatcaTlvTag.InvoiceTimestamp, Encoding.UTF8.GetBytes(timestamp)));
        tlvBytes.AddRange(BuildTlvEntry(ZatcaTlvTag.TotalWithVat, Encoding.UTF8.GetBytes(totalWithVat)));
        tlvBytes.AddRange(BuildTlvEntry(ZatcaTlvTag.VatTotal, Encoding.UTF8.GetBytes(vatTotal)));

        return Convert.ToBase64String(tlvBytes.ToArray());
    }

    /// <inheritdoc />
    public string ComputeInvoiceHash(string xmlContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);

        var bytes = Encoding.UTF8.GetBytes(xmlContent);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <inheritdoc />
    public async Task<ZatcaClearanceResult> RequestClearanceAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var xmlContent = GenerateFatoorahXml(invoice);
        var invoiceHash = ComputeInvoiceHash(xmlContent);
        var qrCodeBase64 = GenerateTlvQrCode(invoice);

        _logger.LogInformation(
            "Requesting ZATCA clearance for invoice {InvoiceNumber} (hash: {Hash})",
            invoice.InvoiceNumber, invoiceHash[..Math.Min(12, invoiceHash.Length)]);

        if (_config.SandboxMode)
        {
            _logger.LogInformation("ZATCA sandbox mode — returning simulated clearance for invoice {InvoiceNumber}", invoice.InvoiceNumber);

            return new ZatcaClearanceResult
            {
                Cleared = true,
                ClearanceId = Guid.NewGuid().ToString("D"),
                QrCodeBase64 = qrCodeBase64,
                InvoiceHash = invoiceHash,
                Timestamp = DateTime.UtcNow
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ZatcaApi");

            var clearanceRequest = new
            {
                invoiceHash,
                uuid = Guid.NewGuid().ToString("D"),
                invoice = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlContent)),
                invoiceCounter = 1,
                deviceSerialNumber = _config.DeviceSerialNumber
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/compliance/v4/invoices/clearance")
            {
                Content = JsonContent.Create(clearanceRequest, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            };

            requestMessage.Headers.Add("X-CSID", _config.Csid);
            requestMessage.Headers.Add("X-SECRET", _config.Secret);
            requestMessage.Headers.Add("Accept", "application/json");

            _logger.LogDebug("Sending clearance request to ZATCA API for invoice {InvoiceNumber}", invoice.InvoiceNumber);

            var response = await client.SendAsync(requestMessage, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "ZATCA clearance failed for invoice {InvoiceNumber}: HTTP {StatusCode} — {Response}",
                    invoice.InvoiceNumber, response.StatusCode, responseContent);

                return new ZatcaClearanceResult
                {
                    Cleared = false,
                    ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseContent}",
                    InvoiceHash = invoiceHash,
                    Timestamp = DateTime.UtcNow
                };
            }

            var apiResponse = JsonSerializer.Deserialize<ZatcaClearanceApiResponse>(responseContent);

            _logger.LogInformation(
                "ZATCA clearance succeeded for invoice {InvoiceNumber}: clearanceId={ClearanceId}",
                invoice.InvoiceNumber, apiResponse?.ClearanceId);

            return new ZatcaClearanceResult
            {
                Cleared = true,
                ClearanceId = apiResponse?.ClearanceId,
                QrCodeBase64 = qrCodeBase64,
                InvoiceHash = invoiceHash,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during ZATCA clearance for invoice {InvoiceNumber}", invoice.InvoiceNumber);

            return new ZatcaClearanceResult
            {
                Cleared = false,
                ErrorMessage = $"Network error: {ex.Message}",
                InvoiceHash = invoiceHash,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout during ZATCA clearance for invoice {InvoiceNumber}", invoice.InvoiceNumber);

            return new ZatcaClearanceResult
            {
                Cleared = false,
                ErrorMessage = "Request timed out",
                InvoiceHash = invoiceHash,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Builds a complete FATOORAH XML document from the provided ZATCA invoice model.
    /// </summary>
    private static string BuildFatoorahXml(ZatcaInvoiceXml invoice)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(XmlNs + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", CacNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", CbcNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ext", ExtNs.NamespaceName),

                // UBL extensions for ZATCA
                new XElement(ExtNs + "UBLExtensions",
                    new XElement(ExtNs + "UBLExtension",
                        new XElement(ExtNs + "ExtensionID", "urn:sa:gov:zatca:en:standard:tax-invoice:simplified"),
                        new XElement(ExtNs + "ExtensionContent",
                            new XElement(QrcNs + "QRCode")))),

                // Document metadata
                new XElement(CbcNs + "ID", invoice.InvoiceNumber),
                new XElement(CbcNs + "IssueDate", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                new XElement(CbcNs + "IssueTime", invoice.InvoiceDate.ToString("HH:mm:ss")),
                new XElement(CbcNs + "InvoiceTypeCode", "381"),
                new XElement(CbcNs + "DocumentCurrencyCode", invoice.Currency),
                new XElement(CbcNs + "TaxCurrencyCode", "SAR"),

                // Supplier (AccountingSupplierParty)
                new XElement(CacNs + "AccountingSupplierParty",
                    new XElement(CacNs + "Party",
                        new XElement(CacNs + "PartyName",
                            new XElement(CbcNs + "Name", invoice.SupplierName)),
                        new XElement(CacNs + "PartyTaxScheme",
                            new XElement(CacNs + "CompanyID", invoice.SupplierVatNumber),
                            new XElement(CacNs + "TaxScheme",
                                new XElement(CbcNs + "ID", "VAT"))))),

                // Customer (AccountingCustomerParty)
                new XElement(CacNs + "AccountingCustomerParty",
                    new XElement(CacNs + "Party",
                        !string.IsNullOrEmpty(invoice.CustomerVatNumber)
                            ? new XElement(CacNs + "PartyTaxScheme",
                                new XElement(CacNs + "CompanyID", invoice.CustomerVatNumber),
                                new XElement(CacNs + "TaxScheme",
                                    new XElement(CbcNs + "ID", "VAT")))
                            : null)),

                // Payment totals
                new XElement(CacNs + "LegalMonetaryTotal",
                    new XElement(CbcNs + "LineExtensionAmount",
                        new XAttribute("currencyID", invoice.Currency),
                        invoice.SubtotalAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(CbcNs + "TaxExclusiveAmount",
                        new XAttribute("currencyID", invoice.Currency),
                        invoice.SubtotalAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(CbcNs + "TaxInclusiveAmount",
                        new XAttribute("currencyID", invoice.Currency),
                        invoice.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)),
                    new XElement(CbcNs + "PayableAmount",
                        new XAttribute("currencyID", invoice.Currency),
                        invoice.TotalAmount.ToString("F2", CultureInfo.InvariantCulture))),

                // Tax total
                new XElement(CacNs + "TaxTotal",
                    new XElement(CacNs + "TaxSubtotal",
                        new XElement(CacNs + "TaxableAmount",
                            new XAttribute("currencyID", invoice.Currency),
                            invoice.SubtotalAmount.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(CacNs + "TaxAmount",
                            new XAttribute("currencyID", invoice.Currency),
                            invoice.VatAmount.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(CacNs + "TaxCategory",
                            new XElement(CbcNs + "ID", SaVatCategoryCode),
                            new XElement(CbcNs + "Percent", invoice.VatRate.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(CacNs + "TaxScheme",
                                new XElement(CbcNs + "ID", "VAT"))))),

                // Invoice lines
                invoice.Lines.Select(line =>
                    new XElement(CacNs + "InvoiceLine",
                        new XElement(CbcNs + "ID", line.Id.ToString(CultureInfo.InvariantCulture)),
                        new XElement(CbcNs + "InvoicedQuantity",
                            new XAttribute("unitCode", "EA"),
                            line.Quantity.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(CbcNs + "LineExtensionAmount",
                            new XAttribute("currencyID", invoice.Currency),
                            line.LineAmount.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement(CacNs + "Item",
                            new XElement(CbcNs + "Name", line.Description),
                            new XElement(CacNs + "ClassifiedTaxCategory",
                                new XElement(CbcNs + "ID", SaVatCategoryCode),
                                new XElement(CbcNs + "Percent", line.VatRate.ToString("F2", CultureInfo.InvariantCulture)),
                                new XElement(CacNs + "TaxScheme",
                                    new XElement(CbcNs + "ID", "VAT")))),
                        new XElement(CacNs + "Price",
                            new XElement(CbcNs + "PriceAmount",
                                new XAttribute("currencyID", invoice.Currency),
                                line.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)))
                ))));

        return doc.Declaration + Environment.NewLine + doc.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Builds a single TLV entry: [Tag (1 byte)] [Length (1 byte)] [Value (N bytes)].
    /// </summary>
    private static byte[] BuildTlvEntry(byte tag, byte[] value)
    {
        var result = new byte[2 + value.Length];
        result[0] = tag;
        result[1] = (byte)value.Length;
        Array.Copy(value, 0, result, 2, value.Length);
        return result;
    }

    /// <summary>
    /// Deserialization model for the ZATCA clearance API response.
    /// </summary>
    private sealed class ZatcaClearanceApiResponse
    {
        [JsonPropertyName("clearanceId")]
        public string? ClearanceId { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
