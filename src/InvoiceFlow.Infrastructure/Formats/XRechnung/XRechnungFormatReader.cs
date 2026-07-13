using System.Globalization;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

namespace InvoiceFlow.Infrastructure.Formats.XRechnung;

/// <summary>
/// Reads an XRechnung 3.0 XML stream and maps it to core entity types.
/// XRechnung is a German CIUS built on UBL 2.1 — the XML structure is identical
/// but constrained by German BG/BT rules (BR-DE-1 through BR-DE-18).
/// </summary>
public sealed class XRechnungFormatReader : IFormatReader
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.XRechnung;

    /// <summary>Read and parse an XRechnung XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Buffer the stream so we can inspect and re-read
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw XML for metadata and format verification
        using var streamReader = new StreamReader(ms, leaveOpen: true);
        var rawXml = await streamReader.ReadToEndAsync(ct);
        ms.Position = 0;

        // Determine Invoice vs CreditNote root element
        var isCreditNote = rawXml.Contains("<CreditNote", StringComparison.OrdinalIgnoreCase);

        UblInvoice? invoice = null;
        UblCreditNote? creditNote = null;

        try
        {
            if (isCreditNote)
            {
                creditNote = DeserializeFromStream<UblCreditNote>(ms);
            }
            else
            {
                invoice = DeserializeFromStream<UblInvoice>(ms);
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize XRechnung XML: {ex.Message}", ex);
        }

        // Map to core entities using the same UBL mapping logic
        var coreInvoice = isCreditNote
            ? MapCreditNoteToInvoice(creditNote!)
            : MapInvoiceToInvoice(invoice!);

        var lines = isCreditNote
            ? MapCreditNoteLines(creditNote!.CreditNoteLines, coreInvoice.Id)
            : MapInvoiceLines(invoice!.InvoiceLines, coreInvoice.Id);

        // Validate XRechnung CustomizationID
        var validationResults = new List<ValidationResult>();
        var customizationId = isCreditNote ? creditNote!.CustomizationId : invoice!.CustomizationId;

        if (!IsXRechnungCustomizationId(customizationId))
        {
            validationResults.Add(new ValidationResult(
                "BR-DE-1",
                $"CustomizationID does not match XRechnung pattern: '{customizationId}'. " +
                "Expected to contain 'xrechnung' or 'kosit'.",
                ValidationSeverity.Warning,
                isCreditNote ? "/CreditNote/CustomizationID" : "/Invoice/CustomizationID",
                customizationId));
        }
        else
        {
            validationResults.Add(new ValidationResult(
                "BR-DE-1",
                "CustomizationID matches XRechnung pattern.",
                ValidationSeverity.Info,
                isCreditNote ? "/CreditNote/CustomizationID" : "/Invoice/CustomizationID",
                customizationId));
        }

        // Build XRechnung-specific metadata
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Format"] = "XRechnung",
            ["Version"] = XRechnungConstants.Version,
            ["CustomizationId"] = customizationId ?? string.Empty,
        };

        var profileId = isCreditNote ? creditNote!.ProfileId : invoice!.ProfileId;
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            metadata["ProfileId"] = profileId;
        }

        var invoiceTypeCode = isCreditNote ? creditNote!.CreditNoteTypeCode : invoice!.InvoiceTypeCode;
        if (!string.IsNullOrWhiteSpace(invoiceTypeCode))
        {
            metadata["InvoiceTypeCode"] = invoiceTypeCode;
        }

        // Extract Leitweg-ID (BuyerReference) — mandatory in XRechnung for B2B
        var buyerRef = isCreditNote ? creditNote!.BuyerReference : invoice!.BuyerReference;
        if (!string.IsNullOrWhiteSpace(buyerRef))
        {
            metadata["LeitwegId"] = buyerRef;
        }

        return new FormatReadResult(coreInvoice, lines, rawXml, metadata, validationResults);
    }

    private static Invoice MapInvoiceToInvoice(UblInvoice u)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = u.Id ?? string.Empty,
            DocumentType = DocumentType.Invoice,
            InvoiceDate = u.IssueDate ?? DateTime.MinValue,
            DueDate = u.DueDate,
            Currency = u.DocumentCurrencyCode ?? "EUR",
        };

        // Supplier (vendor)
        var supplier = u.AccountingSupplierParty?.Party;
        invoice.VendorName = supplier?.PartyName?.Name
            ?? supplier?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.VendorTaxId = supplier?.PartyTaxScheme?.CompanyId
            ?? supplier?.PartyLegalEntity?.CompanyId;

        // Buyer (customer)
        var buyer = u.AccountingCustomerParty?.Party;
        invoice.BuyerName = buyer?.PartyName?.Name
            ?? buyer?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.BuyerTaxId = buyer?.PartyTaxScheme?.CompanyId
            ?? buyer?.PartyLegalEntity?.CompanyId;

        // Totals
        invoice.Subtotal = u.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0m;
        invoice.TotalAmount = u.LegalMonetaryTotal?.TaxInclusiveAmount?.Value ?? 0m;
        invoice.DiscountAmount = u.LegalMonetaryTotal?.AllowanceTotalAmount?.Value;
        invoice.ShippingAmount = u.LegalMonetaryTotal?.ChargeTotalAmount?.Value;

        // Tax total
        invoice.TaxAmount = u.TaxTotals.Count > 0
            ? u.TaxTotals[0].TaxAmount?.Value ?? 0m
            : 0m;

        // BuyerReference = Leitweg-ID in XRechnung
        invoice.ReferenceNumber = u.BuyerReference;

        return invoice;
    }

    private static Invoice MapCreditNoteToInvoice(UblCreditNote c)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = c.Id ?? string.Empty,
            DocumentType = DocumentType.CreditNote,
            InvoiceDate = c.IssueDate ?? DateTime.MinValue,
            DueDate = c.DueDate,
            Currency = c.DocumentCurrencyCode ?? "EUR",
        };

        var supplier = c.AccountingSupplierParty?.Party;
        invoice.VendorName = supplier?.PartyName?.Name
            ?? supplier?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.VendorTaxId = supplier?.PartyTaxScheme?.CompanyId
            ?? supplier?.PartyLegalEntity?.CompanyId;

        var buyer = c.AccountingCustomerParty?.Party;
        invoice.BuyerName = buyer?.PartyName?.Name
            ?? buyer?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.BuyerTaxId = buyer?.PartyTaxScheme?.CompanyId
            ?? buyer?.PartyLegalEntity?.CompanyId;

        invoice.Subtotal = c.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0m;
        invoice.TotalAmount = c.LegalMonetaryTotal?.TaxInclusiveAmount?.Value ?? 0m;
        invoice.DiscountAmount = c.LegalMonetaryTotal?.AllowanceTotalAmount?.Value;
        invoice.ShippingAmount = c.LegalMonetaryTotal?.ChargeTotalAmount?.Value;

        invoice.TaxAmount = c.TaxTotals.Count > 0
            ? c.TaxTotals[0].TaxAmount?.Value ?? 0m
            : 0m;

        invoice.ReferenceNumber = c.BuyerReference;

        return invoice;
    }

    private static List<InvoiceLine> MapInvoiceLines(List<UblInvoiceLine> uLines, Guid invoiceId)
    {
        var lines = new List<InvoiceLine>(uLines.Count);
        for (var i = 0; i < uLines.Count; i++)
        {
            var ul = uLines[i];
            var line = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                LineNumber = int.TryParse(ul.Id, out var lineNum) ? lineNum : i + 1,
                Description = ul.Item?.Description ?? ul.Item?.Name ?? string.Empty,
                ProductCode = ul.Item?.SellersItemIdentification?.Id,
                Quantity = ul.InvoicedQuantity?.Value ?? 0m,
                Unit = ul.InvoicedQuantity?.UnitCode,
                UnitPrice = ul.Price?.PriceAmount?.Value ?? 0m,
                LineTotal = ul.LineExtensionAmount?.Value ?? 0m,
                TaxCategory = ul.Item?.ClassifiedTaxCategory?.Id,
                TaxRate = ul.Item?.ClassifiedTaxCategory?.Percent ?? 0m,
            };
            lines.Add(line);
        }
        return lines;
    }

    private static List<InvoiceLine> MapCreditNoteLines(List<UblInvoiceLine> cLines, Guid invoiceId)
    {
        return MapInvoiceLines(cLines, invoiceId);
    }

    private static bool IsXRechnungCustomizationId(string? customizationId)
    {
        if (string.IsNullOrWhiteSpace(customizationId))
        {
            return false;
        }

        return customizationId.Contains("xrechnung", StringComparison.OrdinalIgnoreCase)
            || customizationId.Contains("kosit", StringComparison.OrdinalIgnoreCase);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
