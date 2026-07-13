using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd;

/// <summary>
/// Validates a ZUGFeRD/Factur-X CII XML stream against EN 16931 business rules and structural requirements.
/// Performs structural validation, namespace verification, and EN 16931 rule checks (BR-1 through BR-9).
/// </summary>
public sealed class ZugferdFormatValidator : IFormatValidator
{
    /// <summary>The format this validator supports.</summary>
    public InvoiceFormat SupportedFormat => InvoiceFormat.Zugferd;

    /// <summary>Validate ZUGFeRD/Factur-X CII XML content against EN 16931 business rules.</summary>
    public async Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;

        var results = new List<ValidationResult>();

        // 1. Structural XML validation — well-formedness
        ValidateXmlStructure(ms, results);
        ms.Position = 0;

        // 2. Check root element is CrossIndustryInvoice in correct namespace
        ValidateRootElement(ms, results);
        ms.Position = 0;

        // 3. Deserialize for business rule validation
        CiiCrossIndustryInvoice? invoice;
        try
        {
            invoice = DeserializeFromStream<CiiCrossIndustryInvoice>(ms);
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new ValidationResult("STRUCT-0", $"XML deserialization failed: {ex.Message}", ValidationSeverity.Error));
            return BuildResult(results);
        }

        // 4. Validate profile from ExchangedDocumentContext
        ValidateProfile(invoice, results);

        // 5. EN 16931 business rule validation
        ValidateBusinessRules(invoice, results);

        return BuildResult(results);
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

    /// <summary>Validate that the root element is CrossIndustryInvoice in the correct CII namespace.</summary>
    private static void ValidateRootElement(MemoryStream stream, List<ValidationResult> results)
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
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "CrossIndustryInvoice" &&
                        reader.NamespaceURI == CiiNamespaces.Rsm)
                    {
                        results.Add(new ValidationResult(
                            "STRUCT-1",
                            "Root element is CrossIndustryInvoice in correct CII namespace.",
                            ValidationSeverity.Info,
                            "/rsm:CrossIndustryInvoice",
                            reader.NamespaceURI));
                    }
                    else
                    {
                        results.Add(new ValidationResult(
                            "STRUCT-1",
                            $"Unexpected root element: '{reader.LocalName}' in namespace '{reader.NamespaceURI}'. " +
                            $"Expected 'CrossIndustryInvoice' in '{CiiNamespaces.Rsm}'.",
                            ValidationSeverity.Error,
                            null,
                            reader.NamespaceURI));
                    }
                    break;
                }
            }
        }
        catch (XmlException)
        {
            // Already caught in structure validation
        }
    }

    /// <summary>Validate the ZUGFeRD profile from ExchangedDocumentContext.</summary>
    private static void ValidateProfile(CiiCrossIndustryInvoice invoice, List<ValidationResult> results)
    {
        var profileUri = invoice.ExchangedDocumentContext?.GuidelineSpecifiedDocumentContextParameter?.Id;

        if (string.IsNullOrWhiteSpace(profileUri))
        {
            results.Add(new ValidationResult(
                "PROFILE-0",
                "No profile/guideline ID found in ExchangedDocumentContext.",
                ValidationSeverity.Warning,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID"));
            return;
        }

        var profile = CiiNamespaces.ParseProfileUri(profileUri);
        if (profile.HasValue)
        {
            results.Add(new ValidationResult(
                "PROFILE-0",
                $"ZUGFeRD profile detected: {profile.Value} ({profileUri}).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID",
                profileUri));
        }
        else
        {
            results.Add(new ValidationResult(
                "PROFILE-0",
                $"Unrecognized profile URI: '{profileUri}'.",
                ValidationSeverity.Warning,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocumentContext/ram:GuidelineSpecifiedDocumentContextParameter/ram:ID",
                profileUri));
        }
    }

    /// <summary>Validate EN 16931 business rules for the CII invoice.</summary>
    private static void ValidateBusinessRules(CiiCrossIndustryInvoice invoice, List<ValidationResult> results)
    {
        var doc = invoice.ExchangedDocument;
        var transaction = invoice.SupplyChainTradeTransaction;
        var agreement = transaction?.ApplicableHeaderTradeAgreement;
        var settlement = transaction?.ApplicableHeaderTradeSettlement;
        var summation = settlement?.SpecifiedTradeSettlementHeaderMonetarySummation;

        // BR-1 (BT-1): Invoice shall have an Invoice number
        if (string.IsNullOrWhiteSpace(doc?.Id))
        {
            results.Add(new ValidationResult(
                "BR-1",
                "Invoice shall have an Invoice number (BT-1).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:ID"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-1",
                "Invoice number present (BT-1).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:ID",
                doc.Id));
        }

        // BR-2 (BT-2): Invoice shall have an Issue date
        if (doc?.IssueDateTime is null || string.IsNullOrWhiteSpace(doc.IssueDateTime.DateTimeString?.Value))
        {
            results.Add(new ValidationResult(
                "BR-2",
                "Invoice shall have an Issue date (BT-2).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:IssueDateTime"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-2",
                "Issue date present (BT-2).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:IssueDateTime",
                doc.IssueDateTime.DateTimeString?.Value));
        }

        // BR-3 (BT-3): Invoice shall have an InvoiceTypeCode
        if (string.IsNullOrWhiteSpace(doc?.TypeCode))
        {
            results.Add(new ValidationResult(
                "BR-3",
                "Invoice shall have an Invoice type code (BT-3).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:TypeCode"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-3",
                "Invoice type code present (BT-3).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:TypeCode",
                doc.TypeCode));
        }

        // BR-4 (BT-5): Invoice shall have an InvoiceCurrencyCode
        if (string.IsNullOrWhiteSpace(settlement?.InvoiceCurrencyCode))
        {
            results.Add(new ValidationResult(
                "BR-4",
                "Invoice shall have an Invoice currency code (BT-5).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:InvoiceCurrencyCode"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-4",
                "Invoice currency code present (BT-5).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:InvoiceCurrencyCode",
                settlement.InvoiceCurrencyCode));
        }

        // BR-5 (BT-27): Invoice shall have a Seller name
        var sellerName = agreement?.SellerTradeParty?.Name
            ?? agreement?.SellerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName;
        if (string.IsNullOrWhiteSpace(sellerName))
        {
            results.Add(new ValidationResult(
                "BR-5",
                "Invoice shall have a Seller name (BT-27).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:SellerTradeParty/ram:Name"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-5",
                "Seller name present (BT-27).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:SellerTradeParty/ram:Name",
                sellerName));
        }

        // BR-6 (BT-44): Invoice shall have a Buyer name
        var buyerName = agreement?.BuyerTradeParty?.Name
            ?? agreement?.BuyerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName;
        if (string.IsNullOrWhiteSpace(buyerName))
        {
            results.Add(new ValidationResult(
                "BR-6",
                "Invoice shall have a Buyer name (BT-44).",
                ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:BuyerTradeParty/ram:Name"));
        }
        else
        {
            results.Add(new ValidationResult(
                "BR-6",
                "Buyer name present (BT-44).",
                ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:BuyerTradeParty/ram:Name",
                buyerName));
        }

        // BR-7, BR-8, BR-CO-15: Per-line validation
        var lineItems = transaction?.IncludedSupplyChainTradeLineItems;
        if (lineItems is { Count: > 0 })
        {
            for (var i = 0; i < lineItems.Count; i++)
            {
                var item = lineItems[i];
                var linePath = $"/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:IncludedSupplyChainTradeLineItem[{i + 1}]";

                // BR-7 (BT-146): Each Invoice line shall have an Item net price
                var price = item.SpecifiedLineTradeAgreement?.NetPriceProductTradePrice?.ChargeAmount?.Value;
                if (!price.HasValue || price.Value == 0)
                {
                    results.Add(new ValidationResult(
                        "BR-7",
                        $"Invoice line {i + 1} shall have an Item net price (BT-146).",
                        ValidationSeverity.Error,
                        $"{linePath}/ram:SpecifiedLineTradeAgreement/ram:NetPriceProductTradePrice/ram:ChargeAmount"));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "BR-7",
                        $"Invoice line {i + 1} has Item net price (BT-146).",
                        ValidationSeverity.Info,
                        $"{linePath}/ram:SpecifiedLineTradeAgreement/ram:NetPriceProductTradePrice/ram:ChargeAmount",
                        price.Value.ToString()));
                }

                // BR-8 (BT-131): Each Invoice line shall have an Invoice line net amount
                var lineAmount = item.SpecifiedLineTradeSettlement?.SpecifiedTradeSettlementLineMonetarySummation?.LineTotalAmount?.Value;
                if (!lineAmount.HasValue || lineAmount.Value == 0)
                {
                    results.Add(new ValidationResult(
                        "BR-8",
                        $"Invoice line {i + 1} shall have an Invoice line net amount (BT-131).",
                        ValidationSeverity.Error,
                        $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount"));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "BR-8",
                        $"Invoice line {i + 1} has Invoice line net amount (BT-131).",
                        ValidationSeverity.Info,
                        $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                        lineAmount.Value.ToString()));
                }

                // BR-CO-15: Invoice line net amount = Quantity × Price
                if (price.HasValue && lineAmount.HasValue && price.Value > 0)
                {
                    var quantity = item.SpecifiedLineTradeDelivery?.BilledQuantity?.Value ?? 0m;
                    var expected = Math.Round(quantity * price.Value, 2, MidpointRounding.AwayFromZero);
                    if (Math.Abs(expected - lineAmount.Value) > 0.02m)
                    {
                        results.Add(new ValidationResult(
                            "BR-CO-15",
                            $"Invoice line {i + 1} net amount ({lineAmount.Value}) does not equal Quantity ({quantity}) × Price ({price.Value}) = {expected}.",
                            ValidationSeverity.Warning,
                            $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                            lineAmount.Value.ToString()));
                    }
                    else
                    {
                        results.Add(new ValidationResult(
                            "BR-CO-15",
                            $"Invoice line {i + 1} net amount matches Quantity × Price.",
                            ValidationSeverity.Info,
                            $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                            lineAmount.Value.ToString()));
                    }
                }
            }
        }

        // BR-9: Tax inclusive amount should be consistent
        if (summation is not null)
        {
            var taxBasis = summation.TaxBasisTotalAmount?.Value ?? 0m;
            var grandTotal = summation.GrandTotalAmount?.Value ?? 0m;
            var taxTotal = summation.TaxTotalAmount?.Value ?? 0m;

            if (taxBasis > 0 && grandTotal > 0 && taxTotal > 0)
            {
                var expectedGrandTotal = Math.Round(taxBasis + taxTotal, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expectedGrandTotal - grandTotal) > 0.02m)
                {
                    results.Add(new ValidationResult(
                        "BR-9",
                        $"Grand total amount ({grandTotal}) does not equal Tax basis ({taxBasis}) + Tax total ({taxTotal}) = {expectedGrandTotal}.",
                        ValidationSeverity.Warning,
                        "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount",
                        grandTotal.ToString()));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "BR-9",
                        "Grand total amount is consistent with Tax basis + Tax total.",
                        ValidationSeverity.Info,
                        "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount",
                        grandTotal.ToString()));
                }
            }
        }
    }

    /// <summary>Build the final validation result, determining overall validity.</summary>
    private static FormatValidationResult BuildResult(List<ValidationResult> results)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, InvoiceFormat.Zugferd);
    }

    /// <summary>Deserialize XML from a stream into an object of type T.</summary>
    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
