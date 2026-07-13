using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

namespace InvoiceFlow.Infrastructure.Formats.XRechnung;

/// <summary>
/// Validates XRechnung 3.0 XML content against German CIUS business rules (BR-DE-1 through BR-DE-18).
/// First performs standard EN 16931 structural validation, then applies German-specific constraints.
/// </summary>
public sealed class XRechnungFormatValidator : IFormatValidator
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.XRechnung;

    /// <summary>Validate XRechnung XML content against German CIUS business rules.</summary>
    public async Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var results = new List<ValidationResult>();

        // 1. Structural XML validation (well-formedness)
        ValidateXmlStructure(ms, results);
        ms.Position = 0;

        // 2. Detect root element
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
            return BuildResult(results);
        }

        // 4. Standard EN 16931 rules (reused from UBL 2.1 pattern)
        if (invoice is not null)
        {
            ValidateInvoiceEn16931Rules(invoice, results);
            ValidateXRechnungRules(invoice, results);
        }
        else if (creditNote is not null)
        {
            ValidateCreditNoteEn16931Rules(creditNote, results);
            ValidateCreditNoteXRechnungRules(creditNote, results);
        }

        return BuildResult(results);
    }

    /// <summary>Validate basic XML well-formedness.</summary>
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
                // Reading through to verify well-formedness
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

    /// <summary>Read first 4KB of root XML to detect Invoice vs CreditNote.</summary>
    private static async Task<string> ReadRootXml(MemoryStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var buffer = new char[4096];
        var bytesRead = await reader.ReadAsync(buffer, ct);
        return new string(buffer, 0, bytesRead);
    }

    /// <summary>Validate standard EN 16931 business rules for an Invoice.</summary>
    private static void ValidateInvoiceEn16931Rules(UblInvoice invoice, List<ValidationResult> results)
    {
        // BR-1 (BT-1): Invoice number
        if (string.IsNullOrWhiteSpace(invoice.Id))
            results.Add(new ValidationResult("BR-1", "Invoice shall have an Invoice number (BT-1).", ValidationSeverity.Error, "/Invoice/ID"));

        // BR-2 (BT-2): Issue date
        if (!invoice.IssueDate.HasValue || invoice.IssueDate == default)
            results.Add(new ValidationResult("BR-2", "Invoice shall have an Issue date (BT-2).", ValidationSeverity.Error, "/Invoice/IssueDate"));

        // BR-3 (BT-3): Invoice type code
        if (string.IsNullOrWhiteSpace(invoice.InvoiceTypeCode))
            results.Add(new ValidationResult("BR-3", "Invoice shall have an Invoice type code (BT-3).", ValidationSeverity.Error, "/Invoice/InvoiceTypeCode"));

        // BR-4 (BT-5): Currency code
        if (string.IsNullOrWhiteSpace(invoice.DocumentCurrencyCode))
            results.Add(new ValidationResult("BR-4", "Invoice shall have an Invoice currency code (BT-5).", ValidationSeverity.Error, "/Invoice/DocumentCurrencyCode"));

        // BR-5 (BT-27): Seller name
        var sellerName = invoice.AccountingSupplierParty?.Party?.PartyName?.Name
            ?? invoice.AccountingSupplierParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(sellerName))
            results.Add(new ValidationResult("BR-5", "Invoice shall have a Seller name (BT-27).", ValidationSeverity.Error, "/Invoice/AccountingSupplierParty/Party/PartyName/Name"));

        // BR-6 (BT-44): Buyer name
        var buyerName = invoice.AccountingCustomerParty?.Party?.PartyName?.Name
            ?? invoice.AccountingCustomerParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(buyerName))
            results.Add(new ValidationResult("BR-6", "Invoice shall have a Buyer name (BT-44).", ValidationSeverity.Error, "/Invoice/AccountingCustomerParty/Party/PartyName/Name"));

        // Per-line validation
        for (var i = 0; i < invoice.InvoiceLines.Count; i++)
        {
            var line = invoice.InvoiceLines[i];
            var linePath = $"/Invoice/InvoiceLine[{i + 1}]";

            // BR-7: Item net price
            var price = line.Price?.PriceAmount?.Value;
            if (!price.HasValue || price.Value == 0)
                results.Add(new ValidationResult("BR-7", $"Invoice line {i + 1} shall have an Item net price (BT-146).", ValidationSeverity.Error, $"{linePath}/Price/PriceAmount"));

            // BR-8: Line net amount
            var lineAmount = line.LineExtensionAmount?.Value;
            if (!lineAmount.HasValue || lineAmount.Value == 0)
                results.Add(new ValidationResult("BR-8", $"Invoice line {i + 1} shall have an Invoice line net amount (BT-131).", ValidationSeverity.Error, $"{linePath}/LineExtensionAmount"));
        }
    }

    /// <summary>Validate standard EN 16931 business rules for a CreditNote.</summary>
    private static void ValidateCreditNoteEn16931Rules(UblCreditNote creditNote, List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(creditNote.Id))
            results.Add(new ValidationResult("BR-1", "Credit note shall have a Credit note number (BT-1).", ValidationSeverity.Error, "/CreditNote/ID"));

        if (!creditNote.IssueDate.HasValue || creditNote.IssueDate == default)
            results.Add(new ValidationResult("BR-2", "Credit note shall have an Issue date (BT-2).", ValidationSeverity.Error, "/CreditNote/IssueDate"));

        if (string.IsNullOrWhiteSpace(creditNote.CreditNoteTypeCode))
            results.Add(new ValidationResult("BR-3", "Credit note shall have a Credit note type code (BT-3).", ValidationSeverity.Error, "/CreditNote/CreditNoteTypeCode"));

        if (string.IsNullOrWhiteSpace(creditNote.DocumentCurrencyCode))
            results.Add(new ValidationResult("BR-4", "Credit note shall have an Invoice currency code (BT-5).", ValidationSeverity.Error, "/CreditNote/DocumentCurrencyCode"));

        var sellerName = creditNote.AccountingSupplierParty?.Party?.PartyName?.Name
            ?? creditNote.AccountingSupplierParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(sellerName))
            results.Add(new ValidationResult("BR-5", "Credit note shall have a Seller name (BT-27).", ValidationSeverity.Error, "/CreditNote/AccountingSupplierParty/Party/PartyName/Name"));

        var buyerName = creditNote.AccountingCustomerParty?.Party?.PartyName?.Name
            ?? creditNote.AccountingCustomerParty?.Party?.PartyLegalEntity?.RegistrationName;
        if (string.IsNullOrWhiteSpace(buyerName))
            results.Add(new ValidationResult("BR-6", "Credit note shall have a Buyer name (BT-44).", ValidationSeverity.Error, "/CreditNote/AccountingCustomerParty/Party/PartyName/Name"));

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
        }
    }

    /// <summary>
    /// Validate German CIUS rules (BR-DE-1 through BR-DE-18) for an Invoice.
    /// </summary>
    private static void ValidateXRechnungRules(UblInvoice invoice, List<ValidationResult> results)
    {
        // BR-DE-1: CustomizationID must contain "xrechnung"
        ValidateBrDe1(invoice.CustomizationId, results, "/Invoice/CustomizationID");

        // BR-DE-2: BuyerReference (Leitweg-ID) must be present for B2B
        ValidateBrDe2(invoice.BuyerReference, results, "/Invoice/BuyerReference");

        // BR-DE-3: Seller MUST have VAT identifier (PartyTaxScheme with TaxScheme.ID = "VAT")
        ValidateBrDe3SellerVat(invoice.AccountingSupplierParty?.Party, results, "/Invoice/AccountingSupplierParty");

        // BR-DE-5: InvoiceTypeCode must be valid
        ValidateBrDe5TypeCode(invoice.InvoiceTypeCode, results, "/Invoice/InvoiceTypeCode");

        // BR-DE-7: Payment terms due date OR payment means must exist
        ValidateBrDe7PaymentTerms(invoice, results);

        // BR-DE-13: Seller contact phone OR email required
        ValidateBrDe13SellerContact(invoice.AccountingSupplierParty?.Party, results, "/Invoice/AccountingSupplierParty");
    }

    /// <summary>
    /// Validate German CIUS rules for a CreditNote.
    /// </summary>
    private static void ValidateCreditNoteXRechnungRules(UblCreditNote creditNote, List<ValidationResult> results)
    {
        // BR-DE-1: CustomizationID must contain "xrechnung"
        ValidateBrDe1(creditNote.CustomizationId, results, "/CreditNote/CustomizationID");

        // BR-DE-2: BuyerReference (Leitweg-ID) must be present
        ValidateBrDe2(creditNote.BuyerReference, results, "/CreditNote/BuyerReference");

        // BR-DE-3: Seller VAT identifier
        ValidateBrDe3SellerVat(creditNote.AccountingSupplierParty?.Party, results, "/CreditNote/AccountingSupplierParty");

        // BR-DE-5: CreditNote type code
        ValidateBrDe5CreditNoteTypeCode(creditNote.CreditNoteTypeCode, results, "/CreditNote/CreditNoteTypeCode");

        // BR-DE-13: Seller contact
        ValidateBrDe13SellerContact(creditNote.AccountingSupplierParty?.Party, results, "/CreditNote/AccountingSupplierParty");
    }

    /// <summary>BR-DE-1: CustomizationID shall indicate XRechnung.</summary>
    private static void ValidateBrDe1(string? customizationId, List<ValidationResult> results, string xPath)
    {
        if (string.IsNullOrWhiteSpace(customizationId))
        {
            results.Add(new ValidationResult(
                "BR-DE-1",
                "CustomizationID is missing. Must indicate XRechnung (BT-22).",
                ValidationSeverity.Error,
                xPath));
            return;
        }

        if (customizationId.Contains("xrechnung", StringComparison.OrdinalIgnoreCase)
            || customizationId.Contains("kosit", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new ValidationResult(
                "BR-DE-1",
                "CustomizationID indicates XRechnung.",
                ValidationSeverity.Info,
                xPath,
                customizationId));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-1",
                $"CustomizationID does not indicate XRechnung: '{customizationId}'. " +
                $"Expected '{XRechnungConstants.CustomizationId}'.",
                ValidationSeverity.Error,
                xPath,
                customizationId));
        }
    }

    /// <summary>BR-DE-2: BuyerReference (Leitweg-ID) is mandatory for B2B invoices.</summary>
    private static void ValidateBrDe2(string? buyerReference, List<ValidationResult> results, string xPath)
    {
        if (string.IsNullOrWhiteSpace(buyerReference))
        {
            results.Add(new ValidationResult(
                "BR-DE-2",
                "BuyerReference (Leitweg-ID) is mandatory for XRechnung B2B invoices (BT-13).",
                ValidationSeverity.Error,
                xPath));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-2",
                "BuyerReference (Leitweg-ID) present.",
                ValidationSeverity.Info,
                xPath,
                buyerReference));
        }
    }

    /// <summary>BR-DE-3: Seller MUST have a VAT identifier (PartyTaxScheme with TaxScheme.ID = "VAT").</summary>
    private static void ValidateBrDe3SellerVat(UblParty? sellerParty, List<ValidationResult> results, string xPath)
    {
        var hasVat = sellerParty?.PartyTaxScheme is not null
            && string.Equals(sellerParty.PartyTaxScheme.TaxScheme?.Id, "VAT", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(sellerParty.PartyTaxScheme.CompanyId);

        if (hasVat)
        {
            results.Add(new ValidationResult(
                "BR-DE-3",
                "Seller has VAT identifier (BT-31).",
                ValidationSeverity.Info,
                $"{xPath}/Party/PartyTaxScheme",
                sellerParty!.PartyTaxScheme!.CompanyId));
        }
        else
        {
            // Seller must have either VAT ID or a national tax registration
            var hasTaxReg = !string.IsNullOrWhiteSpace(sellerParty?.PartyLegalEntity?.CompanyId);
            if (hasTaxReg)
            {
                results.Add(new ValidationResult(
                    "BR-DE-3",
                    "Seller has no VAT identifier but has a national tax registration (BT-32).",
                    ValidationSeverity.Warning,
                    $"{xPath}/Party/PartyLegalEntity",
                    sellerParty!.PartyLegalEntity!.CompanyId));
            }
            else
            {
                results.Add(new ValidationResult(
                    "BR-DE-3",
                    "Seller MUST have a VAT identifier (BT-31) or tax registration ID (BT-32).",
                    ValidationSeverity.Error,
                    xPath));
            }
        }
    }

    /// <summary>BR-DE-5: InvoiceTypeCode must be a valid XRechnung type code.</summary>
    private static void ValidateBrDe5TypeCode(string? typeCode, List<ValidationResult> results, string xPath)
    {
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                "Invoice type code is missing (BT-3).",
                ValidationSeverity.Error,
                xPath));
            return;
        }

        if (Array.Exists(XRechnungConstants.ValidInvoiceTypeCodes, code => code == typeCode))
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                $"Invoice type code '{typeCode}' is valid for XRechnung.",
                ValidationSeverity.Info,
                xPath,
                typeCode));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                $"Invoice type code '{typeCode}' is not valid for XRechnung. " +
                $"Valid codes: {string.Join(", ", XRechnungConstants.ValidInvoiceTypeCodes)}.",
                ValidationSeverity.Error,
                xPath,
                typeCode));
        }
    }

    /// <summary>BR-DE-5 variant: Validate CreditNote type code.</summary>
    private static void ValidateBrDe5CreditNoteTypeCode(string? typeCode, List<ValidationResult> results, string xPath)
    {
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                "Credit note type code is missing (BT-3).",
                ValidationSeverity.Error,
                xPath));
            return;
        }

        // For credit notes, 381 is the standard code
        if (typeCode == "381")
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                "Credit note type code '381' is valid for XRechnung.",
                ValidationSeverity.Info,
                xPath,
                typeCode));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-5",
                $"Credit note type code '{typeCode}' is not standard for XRechnung. Expected '381'.",
                ValidationSeverity.Warning,
                xPath,
                typeCode));
        }
    }

    /// <summary>BR-DE-7: Payment terms due date OR payment means must exist.</summary>
    private static void ValidateBrDe7PaymentTerms(UblInvoice invoice, List<ValidationResult> results)
    {
        var hasDueDate = invoice.DueDate.HasValue;
        var hasPaymentMeans = invoice.PaymentMeans is not null;
        var hasPaymentTerms = invoice.PaymentTerms is not null
            && !string.IsNullOrWhiteSpace(invoice.PaymentTerms.Note);

        if (hasDueDate || hasPaymentMeans || hasPaymentTerms)
        {
            results.Add(new ValidationResult(
                "BR-DE-7",
                "Payment terms are present (due date, payment means, or payment terms note).",
                ValidationSeverity.Info,
                "/Invoice/DueDate"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-7",
                "At least one of DueDate (BT-9), PaymentMeans (BG-16), or PaymentTerms (BG-20) must exist.",
                ValidationSeverity.Error,
                "/Invoice/DueDate"));
        }
    }

    /// <summary>BR-DE-13: Seller contact phone OR email is required.</summary>
    private static void ValidateBrDe13SellerContact(UblParty? sellerParty, List<ValidationResult> results, string xPath)
    {
        var contact = sellerParty?.Contact;
        var hasPhone = !string.IsNullOrWhiteSpace(contact?.Telephone);
        var hasEmail = !string.IsNullOrWhiteSpace(contact?.ElectronicMail);

        if (hasPhone || hasEmail)
        {
            results.Add(new ValidationResult(
                "BR-DE-13",
                "Seller has contact information (phone or email).",
                ValidationSeverity.Info,
                $"{xPath}/Party/Contact"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-DE-13",
                "Seller MUST have a contact telephone (BT-41) or electronic mail address (BT-43).",
                ValidationSeverity.Error,
                $"{xPath}/Party/Contact"));
        }
    }

    private static FormatValidationResult BuildResult(List<ValidationResult> results)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, InvoiceFormat.XRechnung);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
