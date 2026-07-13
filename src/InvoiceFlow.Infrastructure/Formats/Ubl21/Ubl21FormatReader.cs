using System.Globalization;
using System.Xml.Serialization;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21;

/// <summary>
/// Reads a UBL 2.1 Invoice or CreditNote XML stream and maps it to core entity types.
/// Supports both &lt;Invoice&gt; and &lt;CreditNote&gt; root elements.
/// </summary>
public sealed class Ubl21FormatReader : IFormatReader
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.Ubl21;

    /// <summary>Read and parse a UBL 2.1 XML stream into Invoice + InvoiceLine entities.</summary>
    public async Task<FormatReadResult> ReadAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Read stream into memory so we can inspect root element and deserialize
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Read raw XML string for rawXml output and format detection
        using var reader = new StreamReader(ms, leaveOpen: true);
        var rawXml = await reader.ReadToEndAsync(ct);
        ms.Position = 0;

        // Detect root element and deserialize accordingly
        UblInvoice? invoice = null;
        UblCreditNote? creditNote = null;
        bool isCreditNote;

        if (rawXml.Contains("<CreditNote", StringComparison.OrdinalIgnoreCase))
        {
            isCreditNote = true;
            creditNote = DeserializeFromStream<UblCreditNote>(ms);
        }
        else
        {
            isCreditNote = false;
            invoice = DeserializeFromStream<UblInvoice>(ms);
        }

        // Map to core entities
        var coreInvoice = isCreditNote
            ? MapCreditNoteToInvoice(creditNote!)
            : MapInvoiceToInvoice(invoice!);

        var lines = isCreditNote
            ? MapCreditNoteLines(creditNote!.CreditNoteLines, coreInvoice.Id)
            : MapInvoiceLines(invoice!.InvoiceLines, coreInvoice.Id);

        // Collect validation results
        var validationResults = new List<ValidationResult>();
        ValidateUblVersion(isCreditNote ? creditNote!.UblVersionId : invoice!.UblVersionId, validationResults);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (invoice?.CustomizationId is not null or { Length: > 0 })
            metadata["CustomizationId"] = invoice.CustomizationId!;
        if (invoice?.ProfileId is not null or { Length: > 0 })
            metadata["ProfileId"] = invoice.ProfileId!;
        if (invoice?.InvoiceTypeCode is not null or { Length: > 0 })
            metadata["InvoiceTypeCode"] = invoice.InvoiceTypeCode!;

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

        // Buyer reference maps to ReferenceNumber
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

        // Supplier (vendor)
        var supplier = c.AccountingSupplierParty?.Party;
        invoice.VendorName = supplier?.PartyName?.Name
            ?? supplier?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.VendorTaxId = supplier?.PartyTaxScheme?.CompanyId
            ?? supplier?.PartyLegalEntity?.CompanyId;

        // Buyer (customer)
        var buyer = c.AccountingCustomerParty?.Party;
        invoice.BuyerName = buyer?.PartyName?.Name
            ?? buyer?.PartyLegalEntity?.RegistrationName
            ?? string.Empty;
        invoice.BuyerTaxId = buyer?.PartyTaxScheme?.CompanyId
            ?? buyer?.PartyLegalEntity?.CompanyId;

        // Totals
        invoice.Subtotal = c.LegalMonetaryTotal?.TaxExclusiveAmount?.Value ?? 0m;
        invoice.TotalAmount = c.LegalMonetaryTotal?.TaxInclusiveAmount?.Value ?? 0m;
        invoice.DiscountAmount = c.LegalMonetaryTotal?.AllowanceTotalAmount?.Value;
        invoice.ShippingAmount = c.LegalMonetaryTotal?.ChargeTotalAmount?.Value;

        // Tax total
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
        // Credit note lines use the same InvoiceLine structure in UBL
        return MapInvoiceLines(cLines, invoiceId);
    }

    private static void ValidateUblVersion(string? versionId, List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            results.Add(new ValidationResult("UBL-0", "UBL version ID is missing.", ValidationSeverity.Warning, "/Invoice/UBLVersionID"));
        }
        else if (versionId != "2.1")
        {
            results.Add(new ValidationResult("UBL-0", $"Expected UBL version 2.1, got '{versionId}'.", ValidationSeverity.Warning, "/Invoice/UBLVersionID", versionId));
        }
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
