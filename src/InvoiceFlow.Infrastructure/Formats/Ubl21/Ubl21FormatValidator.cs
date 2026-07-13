using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21;

/// <summary>
/// Validates a UBL 2.1 XML stream against EN 16931 business rules and structural requirements.
/// Does not bundle the actual XSD — performs structural validation and EN 16931 rule checks.
/// </summary>
public sealed class Ubl21FormatValidator : IFormatValidator
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.Ubl21;

    /// <summary>Validate UBL 2.1 XML content against EN 16931 business rules.</summary>
    public async Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var results = new List<ValidationResult>();

        // 1. Structural XML validation
        ValidateXmlStructure(ms, results);
        ms.Position = 0;

        // 2. Detect root element and determine format
        var rootXml = await ReadRootXml(ms, ct);
        var isCreditNote = rootXml.Contains("<CreditNote", StringComparison.OrdinalIgnoreCase);

        // 3. Deserialize for business rule validation
        ms.Position = 0;
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
            results.Add(new ValidationResult("STRUCT-0", $"XML deserialization failed: {ex.Message}", ValidationSeverity.Error));
            return BuildResult(results, InvoiceFormat.Ubl21);
        }

        // 4. EN 16931 business rule validation
        if (invoice is not null)
        {
            ValidateInvoiceBusinessRules(invoice, results);
        }
        else if (creditNote is not null)
        {
            ValidateCreditNoteBusinessRules(creditNote, results);
        }

        return BuildResult(results, InvoiceFormat.Ubl21);
    }

    /// <summary>Validate basic XML structure (well-formedness).</summary>
    private static void ValidateXmlStructure(MemoryStream stream, List<ValidationResult> results)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.None,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read())
            {
                // Just reading through to verify well-formedness
            }
        }
        catch (XmlException ex)
        {
            results.Add(new ValidationResult(
                "STRUCT-0",
                $"XML is not well-formed: {ex.Message}",
                ValidationSeverity.Error,
                null,
                $"Line {ex.LineNumber}, Position {ex.LinePosition}"));
        }
    }

    /// <summary>Read the root XML element name to determine Invoice vs CreditNote.</summary>
    private static async Task<string> ReadRootXml(MemoryStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[4096];
        var bytesRead = await reader.ReadAsync(buffer, ct);
        return new string(buffer, 0, bytesRead);
    }

    /// <summary>Validate EN 16931 business rules for a UBL Invoice.</summary>
    private static void ValidateInvoiceBusinessRules(UblInvoice invoice, List<ValidationResult> results)
    {
        // BR-1 (BT-1): Invoice shall have an InvoiceNumber
        if (string.IsNullOrWhiteSpace(invoice.Id))
            results.Add(new ValidationResult("BR-1", "Invoice shall have an Invoice number (BT-1).", ValidationSeverity.Error, "/Invoice/ID"));
        else
            results.Add(new ValidationResult("BR-1", "Invoice number present (BT-1).", ValidationSeverity.Info, "/Invoice/ID", invoice.Id));

        // BR-2 (BT-2): Invoice shall have an IssueDate
        if (!invoice.IssueDate.HasValue || invoice.IssueDate == default)
            results.Add(new ValidationResult("BR-2", "Invoice shall have an Issue date (BT-2).", ValidationSeverity.Error, "/Invoice/IssueDate"));
        else
            results.Add(new ValidationResult("BR-2", "Issue date present (BT-2).", ValidationSeverity.Info, "/Invoice/IssueDate", invoice.IssueDate.Value.ToString("O")));

        // BR-3 (BT-3): Invoice shall have an InvoiceTypeCode
        if (string.IsNullOrWhiteSpace(invoice.InvoiceTypeCode))
            results.Add(new ValidationResult("BR-3", "Invoice shall have an Invoice type code (BT-3).", ValidationSeverity.Error, "/Invoice/InvoiceTypeCode"));
        else
            results.Add(new ValidationResult("BR-3", "Invoice type code present (BT-3).", ValidationSeverity.Info, "/Invoice/InvoiceTypeCode", invoice.InvoiceTypeCode));

        // BR-4 (BT-5): Invoice shall have an InvoiceCurrencyCode
        if (string.IsNullOrWhiteSpace(invoice.DocumentCurrencyCode))
            results.Add(new ValidationResult("BR-4", "Invoice shall have an Invoice currency code (BT-5).", ValidationSeverity.Error, "/Invoice/DocumentCurrencyCode"));
        else
            results.Add(new ValidationResult("BR-4", "Invoice currency code present (BT-5).", ValidationSeverity.Info, "/Invoice/DocumentCurrencyCode", invoice.DocumentCurrencyCode));

        // BR-5 (BT-27): Invoice shall have a Seller name
        var sellerName = invoice.AccountingSupplierParty?.Party?.PartyName?.Name
            ?? invoice.AccountingSupplierParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(sellerName))
            results.Add(new ValidationResult("BR-5", "Invoice shall have a Seller name (BT-27).", ValidationSeverity.Error, "/Invoice/AccountingSupplierParty/Party/PartyName/Name"));
        else
            results.Add(new ValidationResult("BR-5", "Seller name present (BT-27).", ValidationSeverity.Info, "/Invoice/AccountingSupplierParty/Party/PartyName/Name", sellerName));

        // BR-6 (BT-44): Invoice shall have a Buyer name
        var buyerName = invoice.AccountingCustomerParty?.Party?.PartyName?.Name
            ?? invoice.AccountingCustomerParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(buyerName))
            results.Add(new ValidationResult("BR-6", "Invoice shall have a Buyer name (BT-44).", ValidationSeverity.Error, "/Invoice/AccountingCustomerParty/Party/PartyName/Name"));
        else
            results.Add(new ValidationResult("BR-6", "Buyer name present (BT-44).", ValidationSeverity.Info, "/Invoice/AccountingCustomerParty/Party/PartyName/Name", buyerName));

        // BR-7, BR-8, BR-CO-15: Per-line validation
        for (var i = 0; i < invoice.InvoiceLines.Count; i++)
        {
            var line = invoice.InvoiceLines[i];
            var linePath = $"/Invoice/InvoiceLine[{i + 1}]";

            // BR-7 (BT-146): Each Invoice line shall have an Item net price
            var price = line.Price?.PriceAmount?.Value;
            if (!price.HasValue || price.Value == 0)
                results.Add(new ValidationResult("BR-7", $"Invoice line {i + 1} shall have an Item net price (BT-146).", ValidationSeverity.Error, $"{linePath}/Price/PriceAmount"));
            else
                results.Add(new ValidationResult("BR-7", $"Invoice line {i + 1} has Item net price (BT-146).", ValidationSeverity.Info, $"{linePath}/Price/PriceAmount", price.Value.ToString()));

            // BR-8 (BT-131): Each Invoice line shall have an Invoice line net amount
            var lineAmount = line.LineExtensionAmount?.Value;
            if (!lineAmount.HasValue || lineAmount.Value == 0)
                results.Add(new ValidationResult("BR-8", $"Invoice line {i + 1} shall have an Invoice line net amount (BT-131).", ValidationSeverity.Error, $"{linePath}/LineExtensionAmount"));
            else
                results.Add(new ValidationResult("BR-8", $"Invoice line {i + 1} has Invoice line net amount (BT-131).", ValidationSeverity.Info, $"{linePath}/LineExtensionAmount", lineAmount.Value.ToString()));

            // BR-CO-15: Invoice line net amount = Quantity × Price
            if (price.HasValue && lineAmount.HasValue && price.Value > 0)
            {
                var quantity = line.InvoicedQuantity?.Value ?? 0m;
                var expected = Math.Round(quantity * price.Value, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expected - lineAmount.Value) > 0.02m)
                {
                    results.Add(new ValidationResult(
                        "BR-CO-15",
                        $"Invoice line {i + 1} net amount ({lineAmount.Value}) does not equal Quantity ({quantity}) × Price ({price.Value}) = {expected}.",
                        ValidationSeverity.Warning,
                        $"{linePath}/LineExtensionAmount",
                        lineAmount.Value.ToString()));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "BR-CO-15",
                        $"Invoice line {i + 1} net amount matches Quantity × Price.",
                        ValidationSeverity.Info,
                        $"{linePath}/LineExtensionAmount",
                        lineAmount.Value.ToString()));
                }
            }
        }

        // BR-9: Tax inclusive amount should be consistent
        var monetaryTotal = invoice.LegalMonetaryTotal;
        if (monetaryTotal is not null)
        {
            var taxExclusive = monetaryTotal.TaxExclusiveAmount?.Value ?? 0m;
            var taxInclusive = monetaryTotal.TaxInclusiveAmount?.Value ?? 0m;
            var totalTax = invoice.TaxTotals.Count > 0
                ? invoice.TaxTotals[0].TaxAmount?.Value ?? 0m
                : 0m;

            if (taxExclusive > 0 && taxInclusive > 0 && totalTax > 0)
            {
                var expectedTaxInclusive = Math.Round(taxExclusive + totalTax, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expectedTaxInclusive - taxInclusive) > 0.02m)
                {
                    results.Add(new ValidationResult(
                        "BR-9",
                        $"Tax inclusive amount ({taxInclusive}) does not equal Tax exclusive amount ({taxExclusive}) + Tax total ({totalTax}) = {expectedTaxInclusive}.",
                        ValidationSeverity.Warning,
                        "/Invoice/LegalMonetaryTotal/TaxInclusiveAmount",
                        taxInclusive.ToString()));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "BR-9",
                        "Tax inclusive amount is consistent with Tax exclusive amount + Tax total.",
                        ValidationSeverity.Info,
                        "/Invoice/LegalMonetaryTotal/TaxInclusiveAmount",
                        taxInclusive.ToString()));
                }
            }
        }
    }

    /// <summary>Validate EN 16931 business rules for a UBL CreditNote (adapted rules).</summary>
    private static void ValidateCreditNoteBusinessRules(UblCreditNote creditNote, List<ValidationResult> results)
    {
        // BR-1: Credit note number
        if (string.IsNullOrWhiteSpace(creditNote.Id))
            results.Add(new ValidationResult("BR-1", "Credit note shall have a Credit note number (BT-1).", ValidationSeverity.Error, "/CreditNote/ID"));
        else
            results.Add(new ValidationResult("BR-1", "Credit note number present (BT-1).", ValidationSeverity.Info, "/CreditNote/ID", creditNote.Id));

        // BR-2: Issue date
        if (!creditNote.IssueDate.HasValue || creditNote.IssueDate == default)
            results.Add(new ValidationResult("BR-2", "Credit note shall have an Issue date (BT-2).", ValidationSeverity.Error, "/CreditNote/IssueDate"));
        else
            results.Add(new ValidationResult("BR-2", "Issue date present (BT-2).", ValidationSeverity.Info, "/CreditNote/IssueDate", creditNote.IssueDate.Value.ToString("O")));

        // BR-3: Type code
        if (string.IsNullOrWhiteSpace(creditNote.CreditNoteTypeCode))
            results.Add(new ValidationResult("BR-3", "Credit note shall have a Credit note type code (BT-3).", ValidationSeverity.Error, "/CreditNote/CreditNoteTypeCode"));
        else
            results.Add(new ValidationResult("BR-3", "Credit note type code present (BT-3).", ValidationSeverity.Info, "/CreditNote/CreditNoteTypeCode", creditNote.CreditNoteTypeCode));

        // BR-4: Currency
        if (string.IsNullOrWhiteSpace(creditNote.DocumentCurrencyCode))
            results.Add(new ValidationResult("BR-4", "Credit note shall have an Invoice currency code (BT-5).", ValidationSeverity.Error, "/CreditNote/DocumentCurrencyCode"));
        else
            results.Add(new ValidationResult("BR-4", "Invoice currency code present (BT-5).", ValidationSeverity.Info, "/CreditNote/DocumentCurrencyCode", creditNote.DocumentCurrencyCode));

        // BR-5: Seller
        var sellerName = creditNote.AccountingSupplierParty?.Party?.PartyName?.Name
            ?? creditNote.AccountingSupplierParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(sellerName))
            results.Add(new ValidationResult("BR-5", "Credit note shall have a Seller name (BT-27).", ValidationSeverity.Error, "/CreditNote/AccountingSupplierParty/Party/PartyName/Name"));
        else
            results.Add(new ValidationResult("BR-5", "Seller name present (BT-27).", ValidationSeverity.Info, "/CreditNote/AccountingSupplierParty/Party/PartyName/Name", sellerName));

        // BR-6: Buyer
        var buyerName = creditNote.AccountingCustomerParty?.Party?.PartyName?.Name
            ?? creditNote.AccountingCustomerParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(buyerName))
            results.Add(new ValidationResult("BR-6", "Credit note shall have a Buyer name (BT-44).", ValidationSeverity.Error, "/CreditNote/AccountingCustomerParty/Party/PartyName/Name"));
        else
            results.Add(new ValidationResult("BR-6", "Buyer name present (BT-44).", ValidationSeverity.Info, "/CreditNote/AccountingCustomerParty/Party/PartyName/Name", buyerName));

        // BR-7, BR-8, BR-CO-15: Per-line validation
        for (var i = 0; i < creditNote.CreditNoteLines.Count; i++)
        {
            var line = creditNote.CreditNoteLines[i];
            var linePath = $"/CreditNote/CreditNoteLine[{i + 1}]";

            var price = line.Price?.PriceAmount?.Value;
            if (!price.HasValue || price.Value == 0)
                results.Add(new ValidationResult("BR-7", $"Credit note line {i + 1} shall have an Item net price (BT-146).", ValidationSeverity.Error, $"{linePath}/Price/PriceAmount"));

            var lineAmount = line.LineExtensionAmount?.Value;
            if (!lineAmount.HasValue || lineAmount.Value == 0)
                results.Add(new ValidationResult("BR-8", $"Credit note line {i + 1} shall have a Line net amount (BT-131).", ValidationSeverity.Error, $"{linePath}/LineExtensionAmount"));

            if (price.HasValue && lineAmount.HasValue && price.Value > 0)
            {
                var quantity = line.InvoicedQuantity?.Value ?? 0m;
                var expected = Math.Round(quantity * price.Value, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expected - lineAmount.Value) > 0.02m)
                {
                    results.Add(new ValidationResult(
                        "BR-CO-15",
                        $"Credit note line {i + 1} net amount ({lineAmount.Value}) does not equal Quantity ({quantity}) × Price ({price.Value}) = {expected}.",
                        ValidationSeverity.Warning,
                        $"{linePath}/LineExtensionAmount",
                        lineAmount.Value.ToString()));
                }
            }
        }
    }

    private static FormatValidationResult BuildResult(List<ValidationResult> results, InvoiceFormat format)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, format);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
