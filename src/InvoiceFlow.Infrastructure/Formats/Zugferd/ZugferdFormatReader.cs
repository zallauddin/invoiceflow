using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd;

/// <summary>
/// Reads a ZUGFeRD/Factur-X CII XML stream and maps it to core entity types.
/// Supports both raw CII XML and PDF/A-3 files with embedded XML attachments.
/// When a PDF/A-3 file is provided, extracts the embedded factur-x.xml or zugferd-invoice.xml.
/// </summary>
public sealed class ZugferdFormatReader : IFormatReader
{
    /// <summary>The format this reader supports.</summary>
    public InvoiceFormat SupportedFormat => InvoiceFormat.Zugferd;

    /// <summary>Magic bytes for PDF files: %PDF-</summary>
    private static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-

    /// <summary>XML declaration start marker bytes: &lt;?xml</summary>
    private static readonly byte[] XmlDeclarationBytes = [0x3C, 0x3F, 0x78, 0x6D, 0x6C]; // <?xml

    /// <summary>Read and parse a ZUGFeRD/Factur-X CII XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Read stream into memory for inspection and processing
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw bytes for format detection
        var rawBytes = ms.ToArray();
        ms.Position = 0;

        // Check if this is a PDF/A-3 file and extract embedded XML
        byte[] xmlBytes;
        if (IsPdfFile(rawBytes))
        {
            xmlBytes = ExtractEmbeddedXml(rawBytes);
        }
        else
        {
            // Assume the stream IS the CII XML directly
            xmlBytes = rawBytes;
        }

        // Read as XML string
        var rawXml = Encoding.UTF8.GetString(xmlBytes);
        using var xmlStream = new MemoryStream(xmlBytes, false);

        // Deserialize CII XML
        var invoice = DeserializeFromStream<CiiCrossIndustryInvoice>(xmlStream);

        // Map to core entities
        var coreInvoice = MapToInvoice(invoice);
        var lines = MapToInvoiceLines(invoice, coreInvoice.Id);

        // Build metadata
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var profileId = invoice.ExchangedDocumentContext?.GuidelineSpecifiedDocumentContextParameter?.Id;
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            metadata["ProfileId"] = profileId;
            var profile = CiiNamespaces.ParseProfileUri(profileId);
            if (profile.HasValue)
                metadata["Profile"] = profile.Value.ToString();
        }

        var typeCode = invoice.ExchangedDocument?.TypeCode;
        if (!string.IsNullOrWhiteSpace(typeCode))
            metadata["TypeCode"] = typeCode;

        // Collect validation results during read
        var validationResults = new List<ValidationResult>();
        ValidateBasicStructure(invoice, validationResults);

        return new FormatReadResult(coreInvoice, lines, rawXml, metadata, validationResults);
    }

    /// <summary>Check if the byte array starts with PDF magic bytes (%PDF-).</summary>
    private static bool IsPdfFile(byte[] data)
    {
        if (data.Length < PdfMagicBytes.Length)
            return false;

        for (var i = 0; i < PdfMagicBytes.Length; i++)
        {
            if (data[i] != PdfMagicBytes[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Extract embedded XML from a PDF/A-3 file using basic byte scanning.
    /// Searches for the first XML declaration marker (<?xml) within the PDF byte stream.
    /// This is a simplified approach; production use should employ a PDF library.
    /// </summary>
    private static byte[] ExtractEmbeddedXml(byte[] pdfBytes)
    {
        // Find the first <?xml declaration in the PDF byte stream
        var startIndex = FindBytes(pdfBytes, XmlDeclarationBytes, 0);

        if (startIndex < 0)
        {
            throw new InvalidOperationException(
                "PDF/A-3 file does not contain an embedded XML attachment. " +
                "Expected an XML declaration (<?xml) within the PDF stream.");
        }

        // Read from the XML declaration to end of PDF (XML extends to EOF in PDF/A-3)
        // Trim any trailing PDF markers (%%EOF)
        var length = pdfBytes.Length - startIndex;
        var eofMarker = FindBytes(pdfBytes, [0x25, 0x25, 0x45, 0x4F, 0x46], startIndex); // %%EOF
        if (eofMarker >= 0)
        {
            length = eofMarker - startIndex;
        }

        var result = new byte[length];
        Array.Copy(pdfBytes, startIndex, result, 0, length);
        return result;
    }

    /// <summary>Search for a byte pattern within a byte array, starting at the given offset.</summary>
    private static int FindBytes(byte[] haystack, byte[] needle, int startOffset)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return -1;

        var limit = haystack.Length - needle.Length;
        for (var i = startOffset; i <= limit; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>Map a deserialized CII CrossIndustryInvoice to a core Invoice entity.</summary>
    private static Invoice MapToInvoice(CiiCrossIndustryInvoice cii)
    {
        var doc = cii.ExchangedDocument;
        var transaction = cii.SupplyChainTradeTransaction;
        var agreement = transaction?.ApplicableHeaderTradeAgreement;
        var settlement = transaction?.ApplicableHeaderTradeSettlement;
        var summation = settlement?.SpecifiedTradeSettlementHeaderMonetarySummation;

        // Determine document type from TypeCode
        var typeCode = doc?.TypeCode;
        var docType = typeCode == "381" ? DocumentType.CreditNote : DocumentType.Invoice;

        // Parse issue date
        var issueDate = ParseCiiDate(doc?.IssueDateTime) ?? DateTime.MinValue;

        // Parse due date from payment terms
        DateTime? dueDate = ParseCiiDate(agreement?.ApplicablePaymentTerms?.DueDateDateTime);

        // Extract tax total
        var taxTotal = summation?.TaxTotalAmount?.Value ?? 0m;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = doc?.Id ?? string.Empty,
            DocumentType = docType,
            InvoiceDate = issueDate,
            DueDate = dueDate,
            Currency = settlement?.InvoiceCurrencyCode ?? "EUR",

            // Seller
            VendorName = agreement?.SellerTradeParty?.Name
                ?? agreement?.SellerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName
                ?? string.Empty,
            VendorTaxId = GetFirstTaxId(agreement?.SellerTradeParty),

            // Buyer
            BuyerName = agreement?.BuyerTradeParty?.Name
                ?? agreement?.BuyerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName
                ?? string.Empty,
            BuyerTaxId = GetFirstTaxId(agreement?.BuyerTradeParty),

            // Financials
            Subtotal = summation?.TaxBasisTotalAmount?.Value ?? 0m,
            TaxAmount = taxTotal,
            TotalAmount = summation?.GrandTotalAmount?.Value ?? 0m,

            // Reference
            ReferenceNumber = agreement?.BuyerReference,
        };

        // Notes
        if (doc?.IncludedNotes.Count > 0)
        {
            invoice.Notes = string.Join(Environment.NewLine,
                doc.IncludedNotes.Select(n => n.Content).Where(c => !string.IsNullOrWhiteSpace(c)));
        }

        return invoice;
    }

    /// <summary>Map CII line items to core InvoiceLine entities.</summary>
    private static List<InvoiceLine> MapToInvoiceLines(CiiCrossIndustryInvoice cii, Guid invoiceId)
    {
        var lineItems = cii.SupplyChainTradeTransaction?.IncludedSupplyChainTradeLineItems;
        if (lineItems is null or { Count: 0 })
            return [];

        var lines = new List<InvoiceLine>(lineItems.Count);
        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var product = item.SpecifiedTradeProduct;
            var price = item.SpecifiedLineTradeAgreement?.NetPriceProductTradePrice?.ChargeAmount;
            var quantity = item.SpecifiedLineTradeDelivery?.BilledQuantity;
            var lineTotal = item.SpecifiedLineTradeSettlement?.SpecifiedTradeSettlementLineMonetarySummation?.LineTotalAmount;
            var lineTax = item.SpecifiedLineTradeSettlement?.ApplicableTradeTax;

            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                LineNumber = int.TryParse(item.AssociatedDocumentLineDocument?.LineId, out var lineNum) ? lineNum : i + 1,
                Description = product?.Description ?? product?.Name ?? string.Empty,
                ProductCode = product?.SellerAssignedId,
                Quantity = quantity?.Value ?? 0m,
                Unit = quantity?.UnitCode,
                UnitPrice = price?.Value ?? 0m,
                LineTotal = lineTotal?.Value ?? 0m,
                TaxCategory = lineTax?.CategoryCode,
                TaxRate = lineTax?.ApplicablePercent?.Value ?? 0m,
            };
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Parse a CII DateTimeType to a nullable DateTime.</summary>
    private static DateTime? ParseCiiDate(CiiDateTimeType? dateTimeType)
    {
        var dateStr = dateTimeType?.DateTimeString?.Value;
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // CII format "102" = yyyyMMdd
        if (DateTime.TryParseExact(dateStr, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Try ISO 8601 as fallback
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var isoResult))
        {
            return isoResult;
        }

        return null;
    }

    /// <summary>Get the first tax registration ID from a trade party.</summary>
    private static string? GetFirstTaxId(CiiTradeParty? party)
    {
        return party?.SpecifiedTaxRegistrations.Count > 0
            ? party.SpecifiedTaxRegistrations[0].Id
            : null;
    }

    /// <summary>Validate basic structural elements during read.</summary>
    private static void ValidateBasicStructure(CiiCrossIndustryInvoice invoice, List<ValidationResult> results)
    {
        if (invoice.ExchangedDocumentContext?.GuidelineSpecifiedDocumentContextParameter?.Id is null)
        {
            results.Add(new ValidationResult(
                "PROFILE-0",
                "No profile/guideline ID found in ExchangedDocumentContext.",
                ValidationSeverity.Warning,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID"));
        }

        if (string.IsNullOrWhiteSpace(invoice.ExchangedDocument?.Id))
        {
            results.Add(new ValidationResult(
                "BR-1",
                "Invoice number (ID) is missing in ExchangedDocument.",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:ID"));
        }

        if (invoice.ExchangedDocument?.IssueDateTime is null)
        {
            results.Add(new ValidationResult(
                "BR-2",
                "Issue date is missing in ExchangedDocument.",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:IssueDateTime"));
        }
    }

    /// <summary>Deserialize XML from a stream into an object of type T.</summary>
    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
