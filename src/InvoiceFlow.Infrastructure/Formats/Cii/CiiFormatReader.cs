using System.Globalization;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Cii.Models;

namespace InvoiceFlow.Infrastructure.Formats.Cii;

/// <summary>
/// Reads a UN/CEFACT Cross-Industry Invoice (CII) D10B XML stream and maps it to core entity types.
/// Supports standalone CII XML — not embedded ZUGFeRD PDF.
/// </summary>
public sealed class CiiFormatReader : IFormatReader
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.Cii;

    /// <summary>Read and parse a CII XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Read stream into memory so we can deserialize
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw XML string for rawXml output
        using var reader = new StreamReader(ms, leaveOpen: true);
        var rawXml = await reader.ReadToEndAsync(ct);
        ms.Position = 0;

        // Deserialize CII XML
        CiiCrossIndustryInvoice cii;
        try
        {
            cii = DeserializeFromStream<CiiCrossIndustryInvoice>(ms);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize CII XML: {ex.Message}", ex);
        }

        // Map to core entities
        var coreInvoice = MapToInvoice(cii);
        var lines = MapToLines(cii, coreInvoice.Id);

        // Collect validation results (info-level from reader)
        var validationResults = new List<ValidationResult>();
        DetectGuidelineProfile(cii, validationResults);

        // Collect metadata
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (cii.ExchangedDocument?.Id is { Length: > 0 })
            metadata["DocumentId"] = cii.ExchangedDocument.Id;
        if (cii.ExchangedDocument?.TypeCode is { Length: > 0 })
            metadata["TypeCode"] = cii.ExchangedDocument.TypeCode;
        if (cii.ExchangedDocumentContext?.GuidelineSpecifiedDocumentContextParameter?.Id is { Length: > 0 })
            metadata["GuidelineId"] = cii.ExchangedDocumentContext.GuidelineSpecifiedDocumentContextParameter.Id;
        if (cii.SupplyChainTradeTransaction?.ApplicableHeaderTradeAgreement?.BuyerReference is { Length: > 0 })
            metadata["BuyerReference"] = cii.SupplyChainTradeTransaction.ApplicableHeaderTradeAgreement.BuyerReference;

        return new FormatReadResult(coreInvoice, lines, rawXml, metadata, validationResults);
    }

    private static Invoice MapToInvoice(CiiCrossIndustryInvoice cii)
    {
        var exchangedDoc = cii.ExchangedDocument;
        var transaction = cii.SupplyChainTradeTransaction;
        var agreement = transaction?.ApplicableHeaderTradeAgreement;
        var settlement = transaction?.ApplicableHeaderTradeSettlement;
        var summation = settlement?.SpecifiedTradeSettlementHeaderMonetarySummation;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = exchangedDoc?.Id ?? string.Empty,
            DocumentType = MapDocumentType(exchangedDoc?.TypeCode),
            InvoiceDate = ParseIssueDate(exchangedDoc?.IssueDateTime),
            Currency = settlement?.InvoiceCurrencyCode ?? "EUR",
            ReferenceNumber = agreement?.BuyerReference,
        };

        // Seller (vendor)
        var seller = agreement?.SellerTradeParty;
        invoice.VendorName = seller?.Name
            ?? seller?.SpecifiedLegalOrganization?.TradingBusinessName
            ?? string.Empty;
        invoice.VendorTaxId = ExtractTaxId(seller);

        // Buyer
        var buyer = agreement?.BuyerTradeParty;
        invoice.BuyerName = buyer?.Name
            ?? buyer?.SpecifiedLegalOrganization?.TradingBusinessName
            ?? string.Empty;
        invoice.BuyerTaxId = ExtractTaxId(buyer);

        // Financials
        invoice.Subtotal = summation?.LineTotalAmount?.Value ?? summation?.TaxBasisTotalAmount?.Value ?? 0m;
        invoice.TaxAmount = summation?.TaxTotalAmount?.Value ?? 0m;
        invoice.TotalAmount = summation?.GrandTotalAmount?.Value ?? 0m;
        invoice.DiscountAmount = summation?.AllowanceTotalAmount?.Value;
        invoice.ShippingAmount = summation?.ChargeTotalAmount?.Value;

        // Payment due date
        invoice.DueDate = ParseDueDate(settlement?.SpecifiedTradePaymentTerms?.DueDateDateTime);

        // Notes from document-level notes
        var notes = exchangedDoc?.IncludedNotes;
        if (notes is { Count: > 0 })
        {
            invoice.Notes = string.Join(Environment.NewLine, notes
                .Where(n => n.Content is { Length: > 0 })
                .Select(n => n.Content!));
        }

        return invoice;
    }

    private static List<InvoiceLine> MapToLines(CiiCrossIndustryInvoice cii, Guid invoiceId)
    {
        var lineItems = cii.SupplyChainTradeTransaction?.IncludedSupplyChainTradeLineItems;
        if (lineItems is null or { Count: 0 })
        {
            return new List<InvoiceLine>();
        }

        var lines = new List<InvoiceLine>(lineItems.Count);
        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var lineDoc = item.AssociatedDocumentLineDocument;
            var product = item.SpecifiedTradeProduct;
            var delivery = item.SpecifiedLineTradeDelivery;
            var agreement = item.SpecifiedLineTradeAgreement;
            var settlement = item.SpecifiedLineTradeSettlement;
            var lineSummation = settlement?.SpecifiedTradeSettlementLineMonetarySummation;
            var classifiedTax = product?.ClassifiedTradeTax ?? settlement?.ApplicableTradeTax;

            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                LineNumber = int.TryParse(lineDoc?.LineId, out var lineNum) ? lineNum : i + 1,
                Description = product?.Description ?? product?.Name ?? string.Empty,
                ProductCode = product?.SellersItemIdentification?.Id,
                Quantity = delivery?.BilledQuantity?.Value ?? 0m,
                Unit = delivery?.BilledQuantity?.UnitCode,
                UnitPrice = agreement?.NetPriceProductTradePrice?.ChargeAmount?.Value ?? 0m,
                LineTotal = lineSummation?.LineTotalAmount?.Value ?? 0m,
                TaxCategory = classifiedTax?.CategoryCode,
                TaxRate = classifiedTax?.ApplicablePercent ?? 0m,
            };
            lines.Add(line);
        }
        return lines;
    }

    private static DocumentType MapDocumentType(string? typeCode)
    {
        return typeCode switch
        {
            "380" => DocumentType.Invoice,
            "381" => DocumentType.CreditNote,
            "388" => DocumentType.Invoice, // Corrected invoice
            "261" => DocumentType.Invoice, // Self-billed invoice
            _ => DocumentType.Invoice,
        };
    }

    private static DateTime ParseIssueDate(CiiDateTimeType? dateTime)
    {
        if (dateTime?.DateTimeString is null)
        {
            return DateTime.MinValue;
        }

        var format = dateTime.DateTimeString.Format ?? "102";
        var value = dateTime.DateTimeString.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        var culture = CultureInfo.InvariantCulture;
        return format switch
        {
            "102" => DateTime.TryParseExact(value, "yyyyMMdd", culture, DateTimeStyles.None, out var d102) ? d102 : DateTime.MinValue,
            "303" => DateTime.TryParseExact(value, "yyyyMMddHHmm", culture, DateTimeStyles.None, out var d303) ? d303 : DateTime.MinValue,
            "616" => DateTime.TryParse(value, culture, DateTimeStyles.None, out var d616) ? d616 : DateTime.MinValue,
            _ => DateTime.TryParse(value, culture, DateTimeStyles.None, out var dFallback) ? dFallback : DateTime.MinValue,
        };
    }

    private static DateTime? ParseDueDate(CiiDateTimeType? dateTime)
    {
        if (dateTime?.DateTimeString is null)
        {
            return null;
        }

        var result = ParseIssueDate(dateTime);
        return result == DateTime.MinValue ? null : result;
    }

    private static string? ExtractTaxId(CiiTradeParty? party)
    {
        if (party?.SpecifiedTaxRegistrations is null)
        {
            return null;
        }

        // Prefer VAT registration (schemeID="VA")
        var vatReg = party.SpecifiedTaxRegistrations
            .FirstOrDefault(t => string.Equals(t.Id?.SchemeId, "VA", StringComparison.OrdinalIgnoreCase));
        if (vatReg?.Id?.Value is { Length: > 0 })
        {
            return vatReg.Id.Value;
        }

        // Fallback to any tax registration
        return party.SpecifiedTaxRegistrations
            .FirstOrDefault()?.Id?.Value;
    }

    private static void DetectGuidelineProfile(CiiCrossIndustryInvoice cii, List<ValidationResult> results)
    {
        var guidelineId = cii.ExchangedDocumentContext?.GuidelineSpecifiedDocumentContextParameter?.Id;
        if (string.IsNullOrWhiteSpace(guidelineId))
        {
            results.Add(new ValidationResult(
                "CII-PROFILE-0",
                "No guideline profile ID found in ExchangedDocumentContext.",
                ValidationSeverity.Warning,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID"));
            return;
        }

        results.Add(new ValidationResult(
            "CII-PROFILE-0",
            $"Detected guideline profile: {guidelineId}",
            ValidationSeverity.Info,
            "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID",
            guidelineId));
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
