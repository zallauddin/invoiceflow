using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Models.Compliance;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Compliance.Peppol;

/// <summary>
/// PEPPOL BIS 3.0 compliance service — validates invoices against PEPPOL business rules,
/// generates UBL 2.1 XML, and transmits documents to a PEPPOL Access Point via AS4.
/// </summary>
public sealed class PeppolComplianceService : IPeppolComplianceService
{
    private readonly PeppolAccessPointConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PeppolComplianceService> _logger;

    /// <summary>EN 16931 customization identifier for PEPPOL BIS 3.0.</summary>
    private const string En16931CustomizationId = "urn:cen.eu:en16931:2017";

    /// <summary>PEPPOL BIS Billing 01:1.0 profile identifier.</summary>
    private const string PeppolProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    /// <summary>PEPPOL BIS 3.0 process type URI for standard invoices.</summary>
    private const string PeppolInvoiceProcessType =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##" +
        "urn:www.cenbii.eu:transaction:biicoretrdm010:ver2.0#urn:www.peppol.eu:bis:peppol4a:ver2.0";

    /// <summary>PEPPOL BIS 3.0 process type URI for credit notes.</summary>
    private const string PeppolCreditNoteProcessType =
        "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2::CreditNote##" +
        "urn:www.cenbii.eu:transaction:biicoretrdm005:ver2.0#urn:www.peppol.eu:bis:peppol4a:ver2.0";

    /// <summary>Valid ISO 3166-1 alpha-2 country codes (major PEPPOL participating countries).</summary>
    private static readonly HashSet<string> ValidCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
        "NL", "NO", "PL", "PT", "RO", "SE", "SI", "SK", "GB", "CH",
        "AD", "AL", "BA", "IS", "LI", "ME", "MK", "RS", "TR", "UA",
        "AU", "BR", "CA", "CN", "IN", "JP", "KR", "MX", "NZ", "SG",
        "US", "ZA"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PeppolComplianceService"/> class.
    /// </summary>
    /// <param name="config">PEPPOL Access Point configuration options.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients.</param>
    /// <param name="logger">Logger instance.</param>
    public PeppolComplianceService(
        IOptions<PeppolAccessPointConfig> config,
        IHttpClientFactory httpClientFactory,
        ILogger<PeppolComplianceService> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<PeppolValidationResult> ValidateAsync(Invoice invoice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var errors = new List<string>();
        var warnings = new List<string>();

        // BR-1: Invoice number is mandatory (BT-1)
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            errors.Add("BR-1: Invoice number (BT-1) is mandatory and must not be empty.");
        }

        // BR-2: Invoice issue date is mandatory (BT-2)
        if (invoice.InvoiceDate == default)
        {
            errors.Add("BR-2: Invoice issue date (BT-2) is mandatory and must be a valid date.");
        }

        // BR-3: Vendor/supplier name is mandatory (BT-27)
        if (string.IsNullOrWhiteSpace(invoice.VendorName))
        {
            errors.Add("BR-3: Vendor/supplier name (BT-27) is mandatory and must not be empty.");
        }

        // BR-4: Buyer name is mandatory (BT-44)
        if (string.IsNullOrWhiteSpace(invoice.BuyerName))
        {
            errors.Add("BR-4: Buyer name (BT-44) is mandatory and must not be empty.");
        }

        // BR-5: Currency code is mandatory and must be valid ISO 4217 (BT-5)
        if (string.IsNullOrWhiteSpace(invoice.Currency))
        {
            errors.Add("BR-5: Currency code (BT-5) is mandatory and must not be empty.");
        }
        else if (!IsValidIso4217Currency(invoice.Currency))
        {
            errors.Add($"BR-5: Currency code '{invoice.Currency}' is not a valid ISO 4217 currency code.");
        }

        // BR-6: Must have at least one line item with a positive amount
        if (invoice.Lines.Count == 0)
        {
            errors.Add("BR-6: At least one invoice line item (BG-25) is mandatory.");
        }
        else
        {
            var hasPositiveLine = invoice.Lines.Any(l => l.LineTotal > 0);
            if (!hasPositiveLine)
            {
                errors.Add("BR-6: At least one invoice line must have a total amount greater than zero.");
            }
        }

        // BR-7: Subtotal + TaxAmount must equal TotalAmount (BT-106 + BT-110 = BT-112)
        if (invoice.Subtotal > 0 || invoice.TaxAmount > 0 || invoice.TotalAmount > 0)
        {
            var expectedTotal = invoice.Subtotal + invoice.TaxAmount;
            if (Math.Abs(expectedTotal - invoice.TotalAmount) > 0.01m)
            {
                errors.Add(
                    $"BR-7: Subtotal ({invoice.Subtotal:F2}) + TaxAmount ({invoice.TaxAmount:F2}) " +
                    $"must equal TotalAmount ({invoice.TotalAmount:F2}). Expected: {expectedTotal:F2}.");
            }
        }

        // BR-8: Country code must be valid ISO 3166-1 alpha-2 if provided (BT-40 / BT-55)
        if (!string.IsNullOrWhiteSpace(invoice.CountryCode))
        {
            if (invoice.CountryCode.Length != 2 || !ValidCountryCodes.Contains(invoice.CountryCode))
            {
                errors.Add(
                    $"BR-8: Country code '{invoice.CountryCode}' is not a valid ISO 3166-1 alpha-2 code.");
            }
        }

        // Warning: VendorTaxId is recommended (BT-31)
        if (string.IsNullOrWhiteSpace(invoice.VendorTaxId))
        {
            warnings.Add("WT-1: Vendor tax identification number (BT-31) is recommended for PEPPOL BIS 3.0 compliance.");
        }

        // Warning: BuyerTaxId is recommended (BT-48)
        if (string.IsNullOrWhiteSpace(invoice.BuyerTaxId))
        {
            warnings.Add("WT-2: Buyer tax identification number (BT-48) is recommended for PEPPOL BIS 3.0 compliance.");
        }

        // Warning: DueDate is recommended (BT-9)
        if (!invoice.DueDate.HasValue)
        {
            warnings.Add("WT-3: Payment due date (BT-9) is recommended for PEPPOL BIS 3.0 compliance.");
        }

        var result = new PeppolValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Timestamp = DateTime.UtcNow
        };

        if (result.IsValid)
        {
            _logger.LogInformation(
                "Invoice {InvoiceNumber} passed PEPPOL BIS 3.0 validation with {WarningCount} warning(s).",
                invoice.InvoiceNumber, warnings.Count);
        }
        else
        {
            _logger.LogWarning(
                "Invoice {InvoiceNumber} failed PEPPOL BIS 3.0 validation with {ErrorCount} error(s).",
                invoice.InvoiceNumber, errors.Count);
        }

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public string GenerateUblXml(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var currency = invoice.Currency ?? "EUR";
        var isCreditNote = invoice.DocumentType == Core.Enums.DocumentType.CreditNote;

        if (isCreditNote)
        {
            var creditNote = MapToCreditNote(invoice, currency);
            return SerializeToXmlString(creditNote, UblNamespaces.CreditNote);
        }

        var ublInvoice = MapToInvoice(invoice, currency);
        return SerializeToXmlString(ublInvoice, UblNamespaces.Invoice);
    }
    /// <inheritdoc />
    public async Task<PeppolTransmissionResult> TransmitAsync(
        Invoice invoice, string ublXml, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentException.ThrowIfNullOrWhiteSpace(ublXml);

        if (_config.SandboxMode)
        {
            _logger.LogInformation(
                "Sandbox mode: simulating PEPPOL transmission for invoice {InvoiceNumber}.",
                invoice.InvoiceNumber);

            return new PeppolTransmissionResult
            {
                Success = true,
                TransmissionId = $"SANDBOX-{Guid.NewGuid():N}",
                Timestamp = DateTime.UtcNow
            };
        }

        if (string.IsNullOrWhiteSpace(_config.EndpointUrl))
        {
            return new PeppolTransmissionResult
            {
                Success = false,
                ErrorMessage = "PEPPOL Access Point endpoint URL is not configured.",
                Timestamp = DateTime.UtcNow
            };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("PeppolAccessPoint");

            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var isCreditNote = invoice.DocumentType == Core.Enums.DocumentType.CreditNote;
            var mediaType = isCreditNote
                ? "application/vnd.peppol creditnote+xml"
                : "application/vnd.peppol invoice+xml";

            var content = new StringContent(ublXml, Encoding.UTF8, mediaType);

            _logger.LogInformation(
                "Transmitting invoice {InvoiceNumber} to PEPPOL Access Point at {EndpointUrl}.",
                invoice.InvoiceNumber, _config.EndpointUrl);

            var response = await client.PostAsync(_config.EndpointUrl, content, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var transmissionId = !string.IsNullOrWhiteSpace(responseBody)
                    ? responseBody
                    : $"AP-{Guid.NewGuid():N}";

                _logger.LogInformation(
                    "Invoice {InvoiceNumber} successfully transmitted. TransmissionId: {TransmissionId}.",
                    invoice.InvoiceNumber, transmissionId);

                return new PeppolTransmissionResult
                {
                    Success = true,
                    TransmissionId = transmissionId,
                    Timestamp = DateTime.UtcNow
                };
            }

            var errorMessage = $"Access Point returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}.";
            _logger.LogWarning(
                "Invoice {InvoiceNumber} transmission failed: {ErrorMessage}.",
                invoice.InvoiceNumber, errorMessage);

            return new PeppolTransmissionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP error transmitting invoice {InvoiceNumber} to PEPPOL Access Point.",
                invoice.InvoiceNumber);

            return new PeppolTransmissionResult
            {
                Success = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "Timeout transmitting invoice {InvoiceNumber} to PEPPOL Access Point.",
                invoice.InvoiceNumber);

            return new PeppolTransmissionResult
            {
                Success = false,
                ErrorMessage = $"Transmission timed out: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>Maps an Invoice entity to a UBL 2.1 Invoice XML model.</summary>
    private static UblInvoice MapToInvoice(Invoice invoice, string currency)
    {
        var ublInvoice = new UblInvoice
        {
            UblVersionId = "2.1",
            CustomizationId = En16931CustomizationId,
            ProfileId = PeppolProfileId,
            Id = invoice.InvoiceNumber,
            IssueDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            InvoiceTypeCode = "380",
            DocumentCurrencyCode = currency,
            BuyerReference = invoice.ReferenceNumber,
        };

        // Supplier (vendor)
        ublInvoice.AccountingSupplierParty = new UblSupplierParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.VendorName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.VendorTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.VendorTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.VendorName,
                    CompanyId = invoice.VendorTaxId,
                },
            }
        };

        // Buyer (customer)
        ublInvoice.AccountingCustomerParty = new UblCustomerParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.BuyerName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.BuyerTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.BuyerTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.BuyerName,
                    CompanyId = invoice.BuyerTaxId,
                },
            }
        };

        // Map lines
        ublInvoice.InvoiceLines = new List<UblInvoiceLine>(invoice.Lines.Count);
        foreach (var line in invoice.Lines)
        {
            ublInvoice.InvoiceLines.Add(MapLine(line, currency));
        }

        // Tax totals — group by TaxCategory
        var taxGroups = invoice.Lines
            .Where(l => l.TaxRate > 0)
            .GroupBy(l => l.TaxCategory ?? "S")
            .ToList();

        if (taxGroups.Count > 0 || invoice.TaxAmount > 0)
        {
            var taxTotal = new UblTaxTotal
            {
                TaxAmount = new UblAmountType { Value = invoice.TaxAmount, CurrencyId = currency }
            };

            foreach (var group in taxGroups)
            {
                taxTotal.TaxSubtotals.Add(new UblTaxSubtotal
                {
                    TaxableAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.LineTotal),
                        CurrencyId = currency
                    },
                    TaxAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.TaxAmount),
                        CurrencyId = currency
                    },
                    TaxCategory = new UblTaxCategory
                    {
                        Id = group.Key,
                        Percent = group.First().TaxRate,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    }
                });
            }

            ublInvoice.TaxTotals.Add(taxTotal);
        }

        // Legal monetary total
        var lineExtension = invoice.Lines.Sum(l => l.LineTotal);
        var taxExclusive = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxInclusive = invoice.TotalAmount > 0 ? invoice.TotalAmount : taxExclusive + invoice.TaxAmount;

        ublInvoice.LegalMonetaryTotal = new UblMonetaryTotal
        {
            LineExtensionAmount = new UblAmountType { Value = lineExtension, CurrencyId = currency },
            TaxExclusiveAmount = new UblAmountType { Value = taxExclusive, CurrencyId = currency },
            TaxInclusiveAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
            PayableAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
        };

        if (invoice.DiscountAmount is > 0)
        {
            ublInvoice.LegalMonetaryTotal.AllowanceTotalAmount = new UblAmountType
            {
                Value = invoice.DiscountAmount.Value,
                CurrencyId = currency
            };
        }

        if (invoice.ShippingAmount is > 0)
        {
            ublInvoice.LegalMonetaryTotal.ChargeTotalAmount = new UblAmountType
            {
                Value = invoice.ShippingAmount.Value,
                CurrencyId = currency
            };
        }

        return ublInvoice;
    }
    /// <summary>Maps an Invoice entity to a UBL 2.1 CreditNote XML model.</summary>
    private static UblCreditNote MapToCreditNote(Invoice invoice, string currency)
    {
        var creditNote = new UblCreditNote
        {
            UblVersionId = "2.1",
            CustomizationId = En16931CustomizationId,
            ProfileId = PeppolProfileId,
            Id = invoice.InvoiceNumber,
            IssueDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            CreditNoteTypeCode = "381",
            DocumentCurrencyCode = currency,
            BuyerReference = invoice.ReferenceNumber,
        };

        // Supplier
        creditNote.AccountingSupplierParty = new UblSupplierParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.VendorName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.VendorTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.VendorTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.VendorName,
                    CompanyId = invoice.VendorTaxId,
                },
            }
        };

        // Buyer
        creditNote.AccountingCustomerParty = new UblCustomerParty
        {
            Party = new UblParty
            {
                PartyName = new UblPartyName { Name = invoice.BuyerName },
                PartyTaxScheme = string.IsNullOrEmpty(invoice.BuyerTaxId)
                    ? null
                    : new UblPartyTaxScheme
                    {
                        CompanyId = invoice.BuyerTaxId,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    },
                PartyLegalEntity = new UblPartyLegalEntity
                {
                    RegistrationName = invoice.BuyerName,
                    CompanyId = invoice.BuyerTaxId,
                },
            }
        };

        // Lines
        creditNote.CreditNoteLines = new List<UblInvoiceLine>(invoice.Lines.Count);
        foreach (var line in invoice.Lines)
        {
            creditNote.CreditNoteLines.Add(MapLine(line, currency));
        }

        // Tax totals
        var taxGroups = invoice.Lines
            .Where(l => l.TaxRate > 0)
            .GroupBy(l => l.TaxCategory ?? "S")
            .ToList();

        if (taxGroups.Count > 0 || invoice.TaxAmount > 0)
        {
            var taxTotal = new UblTaxTotal
            {
                TaxAmount = new UblAmountType { Value = invoice.TaxAmount, CurrencyId = currency }
            };

            foreach (var group in taxGroups)
            {
                taxTotal.TaxSubtotals.Add(new UblTaxSubtotal
                {
                    TaxableAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.LineTotal),
                        CurrencyId = currency
                    },
                    TaxAmount = new UblAmountType
                    {
                        Value = group.Sum(l => l.TaxAmount),
                        CurrencyId = currency
                    },
                    TaxCategory = new UblTaxCategory
                    {
                        Id = group.Key,
                        Percent = group.First().TaxRate,
                        TaxScheme = new UblTaxScheme { Id = "VAT" }
                    }
                });
            }

            creditNote.TaxTotals.Add(taxTotal);
        }

        // Monetary total
        var lineExtension = invoice.Lines.Sum(l => l.LineTotal);
        var taxExclusive = invoice.Subtotal > 0 ? invoice.Subtotal : lineExtension;
        var taxInclusive = invoice.TotalAmount > 0 ? invoice.TotalAmount : taxExclusive + invoice.TaxAmount;

        creditNote.LegalMonetaryTotal = new UblMonetaryTotal
        {
            LineExtensionAmount = new UblAmountType { Value = lineExtension, CurrencyId = currency },
            TaxExclusiveAmount = new UblAmountType { Value = taxExclusive, CurrencyId = currency },
            TaxInclusiveAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
            PayableAmount = new UblAmountType { Value = taxInclusive, CurrencyId = currency },
        };

        return creditNote;
    }

    /// <summary>Maps an InvoiceLine entity to a UBL 2.1 InvoiceLine XML model.</summary>
    private static UblInvoiceLine MapLine(InvoiceLine line, string currency)
    {
        return new UblInvoiceLine
        {
            Id = line.LineNumber.ToString(CultureInfo.InvariantCulture),
            InvoicedQuantity = new UblMeasureType
            {
                Value = line.Quantity,
                UnitCode = line.Unit ?? "EA"
            },
            LineExtensionAmount = new UblAmountType
            {
                Value = line.LineTotal,
                CurrencyId = currency
            },
            Item = new UblItem
            {
                Description = line.Description,
                Name = line.Description,
                SellersItemIdentification = string.IsNullOrEmpty(line.ProductCode)
                    ? null
                    : new UblItemIdentification { Id = line.ProductCode },
                ClassifiedTaxCategory = new UblTaxCategory
                {
                    Id = line.TaxCategory ?? "S",
                    Percent = line.TaxRate,
                    TaxScheme = new UblTaxScheme { Id = "VAT" }
                },
            },
            Price = new UblPrice
            {
                PriceAmount = new UblAmountType
                {
                    Value = line.UnitPrice,
                    CurrencyId = currency
                }
            },
        };
    }

    /// <summary>Serializes a UBL model object to an indented XML string.</summary>
    private static string SerializeToXmlString<T>(T document, string targetNamespace) where T : class
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("cbc", UblNamespaces.Cbc);
        ns.Add("cac", UblNamespaces.Cac);

        var serializer = new XmlSerializer(typeof(T));

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            Encoding = System.Text.Encoding.UTF8,
        };

        using var stream = new MemoryStream();
        using var streamWriter = new StreamWriter(stream, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
        using var xmlWriter = XmlWriter.Create(streamWriter, settings);
        xmlWriter.WriteStartDocument();
        serializer.Serialize(xmlWriter, document, ns);
        xmlWriter.Flush();
        stream.Position = 0;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Checks whether the given code is a valid ISO 4217 currency code.</summary>
    private static bool IsValidIso4217Currency(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
        {
            return false;
        }

        return CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Any(culture =>
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    return string.Equals(region.ISOCurrencySymbol, currencyCode, StringComparison.OrdinalIgnoreCase);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            });
    }
}
