using System.Xml;
using System.Xml.Serialization;
using InvoiceFlow.Formats.Abstractions;
using InvoiceFlow.Infrastructure.Formats.Cii.Models;

namespace InvoiceFlow.Infrastructure.Formats.Cii;

/// <summary>
/// Validates a CII XML stream against D10B structural requirements and EN 16931 business rules
/// adapted for CII XPaths.
/// </summary>
public sealed class CiiFormatValidator : IFormatValidator
{
    /// <inheritdoc />
    public InvoiceFormat SupportedFormat => InvoiceFormat.Cii;

    /// <summary>Validate CII XML content against structural and business rules.</summary>
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

        // 2. Validate root namespace is CII
        ValidateRootNamespace(ms, results);
        ms.Position = 0;

        // 3. Deserialize for business rule validation
        CiiCrossIndustryInvoice? cii;
        try
        {
            cii = DeserializeFromStream<CiiCrossIndustryInvoice>(ms);
        }
        catch (InvalidOperationException ex)
        {
            results.Add(new ValidationResult("STRUCT-1", $"XML deserialization failed: {ex.Message}", ValidationSeverity.Error));
            return BuildResult(results);
        }

        // 4. CII business rules (EN 16931 adapted for CII)
        ValidateBusinessRules(cii, results);

        return BuildResult(results);
    }

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
                // Read through to verify well-formedness
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

    private static void ValidateRootNamespace(MemoryStream stream, List<ValidationResult> results)
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
                    if (reader.LocalName == "CrossIndustryInvoice")
                    {
                        var ns = reader.NamespaceURI;
                        if (string.Equals(ns, CiiNamespaces.Rsm, StringComparison.Ordinal))
                        {
                            results.Add(new ValidationResult(
                                "NS-0",
                                "Root namespace matches CII D10B RSM.",
                                ValidationSeverity.Info,
                                "/rsm:CrossIndustryInvoice",
                                ns));
                        }
                        else
                        {
                            results.Add(new ValidationResult(
                                "NS-0",
                                $"Root namespace '{ns}' does not match expected CII RSM namespace '{CiiNamespaces.Rsm}'.",
                                ValidationSeverity.Error,
                                "/rsm:CrossIndustryInvoice",
                                ns));
                        }
                        break;
                    }

                    // If first element is not CrossIndustryInvoice
                    results.Add(new ValidationResult(
                        "NS-0",
                        $"Expected root element 'CrossIndustryInvoice', got '{reader.LocalName}'.",
                        ValidationSeverity.Error,
                        null,
                        reader.LocalName));
                    break;
                }
            }
        }
        catch (XmlException ex)
        {
            results.Add(new ValidationResult("NS-0", $"Failed to read root namespace: {ex.Message}", ValidationSeverity.Error));
        }
    }

    private static void ValidateBusinessRules(CiiCrossIndustryInvoice cii, List<ValidationResult> results)
    {
        var exchangedDoc = cii.ExchangedDocument;
        var transaction = cii.SupplyChainTradeTransaction;
        var agreement = transaction?.ApplicableHeaderTradeAgreement;
        var settlement = transaction?.ApplicableHeaderTradeSettlement;
        var summation = settlement?.SpecifiedTradeSettlementHeaderMonetarySummation;

        // CII-BR-1: ExchangedDocument/ID present
        if (string.IsNullOrWhiteSpace(exchangedDoc?.Id))
        {
            results.Add(new ValidationResult("CII-BR-1", "Invoice shall have an Invoice number (BT-1 / CII-BR-1).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:ID"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-1", $"Invoice number present: {exchangedDoc.Id} (BT-1).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:ID", exchangedDoc.Id));
        }

        // CII-BR-2: ExchangedDocument/IssueDateTime present
        if (exchangedDoc?.IssueDateTime?.DateTimeString is null || string.IsNullOrWhiteSpace(exchangedDoc.IssueDateTime.DateTimeString.Value))
        {
            results.Add(new ValidationResult("CII-BR-2", "Invoice shall have an Issue date (BT-2 / CII-BR-2).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:IssueDateTime"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-2", $"Issue date present: {exchangedDoc.IssueDateTime.DateTimeString.Value} (BT-2).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:IssueDateTime",
                exchangedDoc.IssueDateTime.DateTimeString.Value));
        }

        // CII-BR-3: ExchangedDocument/TypeCode present
        if (string.IsNullOrWhiteSpace(exchangedDoc?.TypeCode))
        {
            results.Add(new ValidationResult("CII-BR-3", "Invoice shall have an Invoice type code (BT-3 / CII-BR-3).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:TypeCode"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-3", $"Type code present: {exchangedDoc.TypeCode} (BT-3).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:ExchangedDocument/ram:TypeCode", exchangedDoc.TypeCode));
        }

        // CII-BR-5: SellerTradeParty/Name present
        var sellerName = agreement?.SellerTradeParty?.Name
            ?? agreement?.SellerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName;
        if (string.IsNullOrWhiteSpace(sellerName))
        {
            results.Add(new ValidationResult("CII-BR-5", "Invoice shall have a Seller name (BT-27 / CII-BR-5).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:SellerTradeParty/ram:Name"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-5", $"Seller name present: {sellerName} (BT-27).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:SellerTradeParty/ram:Name",
                sellerName));
        }

        // CII-BR-6: BuyerTradeParty/Name present
        var buyerName = agreement?.BuyerTradeParty?.Name
            ?? agreement?.BuyerTradeParty?.SpecifiedLegalOrganization?.TradingBusinessName;
        if (string.IsNullOrWhiteSpace(buyerName))
        {
            results.Add(new ValidationResult("CII-BR-6", "Invoice shall have a Buyer name (BT-44 / CII-BR-6).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:BuyerTradeParty/ram:Name"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-6", $"Buyer name present: {buyerName} (BT-44).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeAgreement/ram:BuyerTradeParty/ram:Name",
                buyerName));
        }

        // CII-BR-8: GrandTotalAmount present
        var grandTotal = summation?.GrandTotalAmount?.Value;
        if (!grandTotal.HasValue)
        {
            results.Add(new ValidationResult("CII-BR-8", "Invoice shall have a Grand total amount (BT-112 / CII-BR-8).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-8", $"Grand total present: {grandTotal.Value} (BT-112).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount",
                grandTotal.Value.ToString()));
        }

        // CII-BR-9: GrandTotalAmount ≈ LineTotalAmount + TaxTotalAmount
        var lineTotal = summation?.LineTotalAmount?.Value ?? summation?.TaxBasisTotalAmount?.Value;
        var taxTotal = summation?.TaxTotalAmount?.Value;
        if (grandTotal.HasValue && lineTotal.HasValue && taxTotal.HasValue && lineTotal.Value > 0 && taxTotal.Value > 0)
        {
            var expected = Math.Round(lineTotal.Value + taxTotal.Value, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(expected - grandTotal.Value) > 0.02m)
            {
                results.Add(new ValidationResult(
                    "CII-BR-9",
                    $"Grand total ({grandTotal.Value}) does not equal Line total ({lineTotal.Value}) + Tax total ({taxTotal.Value}) = {expected}.",
                    ValidationSeverity.Warning,
                    "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount",
                    grandTotal.Value.ToString()));
            }
            else
            {
                results.Add(new ValidationResult(
                    "CII-BR-9",
                    "Grand total is consistent with Line total + Tax total.",
                    ValidationSeverity.Info,
                    "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:SpecifiedTradeSettlementHeaderMonetarySummation/ram:GrandTotalAmount",
                    grandTotal.Value.ToString()));
            }
        }

        // CII-BR-0: Currency code present
        if (string.IsNullOrWhiteSpace(settlement?.InvoiceCurrencyCode))
        {
            results.Add(new ValidationResult("CII-BR-0", "Invoice shall have an Invoice currency code (BT-5).", ValidationSeverity.Error,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:InvoiceCurrencyCode"));
        }
        else
        {
            results.Add(new ValidationResult("CII-BR-0", $"Currency code present: {settlement.InvoiceCurrencyCode} (BT-5).", ValidationSeverity.Info,
                "/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:ApplicableHeaderTradeSettlement/ram:InvoiceCurrencyCode",
                settlement.InvoiceCurrencyCode));
        }

        // Per-line validation
        var lineItems = transaction?.IncludedSupplyChainTradeLineItems;
        if (lineItems is { Count: > 0 })
        {
            ValidateLineItems(lineItems, results);
        }
    }

    private static void ValidateLineItems(List<CiiTradeLineItem> lineItems, List<ValidationResult> results)
    {
        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var linePath = $"/rsm:CrossIndustryInvoice/rsm:SupplyChainTradeTransaction/ram:IncludedSupplyChainTradeLineItem[{i + 1}]";

            // CII-BR-7: Each line shall have a net price (BT-146)
            var netPrice = item.SpecifiedLineTradeAgreement?.NetPriceProductTradePrice?.ChargeAmount?.Value;
            if (!netPrice.HasValue || netPrice.Value == 0)
            {
                results.Add(new ValidationResult("CII-BR-7",
                    $"Line {i + 1} shall have a net price (BT-146).",
                    ValidationSeverity.Error,
                    $"{linePath}/ram:SpecifiedLineTradeAgreement/ram:NetPriceProductTradePrice/ram:ChargeAmount"));
            }
            else
            {
                results.Add(new ValidationResult("CII-BR-7",
                    $"Line {i + 1} net price present: {netPrice.Value} (BT-146).",
                    ValidationSeverity.Info,
                    $"{linePath}/ram:SpecifiedLineTradeAgreement/ram:NetPriceProductTradePrice/ram:ChargeAmount",
                    netPrice.Value.ToString()));
            }

            // CII-BR-10: Each line shall have a line net amount (BT-131)
            var lineTotalAmount = item.SpecifiedLineTradeSettlement?
                .SpecifiedTradeSettlementLineMonetarySummation?.LineTotalAmount?.Value;
            if (!lineTotalAmount.HasValue || lineTotalAmount.Value == 0)
            {
                results.Add(new ValidationResult("CII-BR-10",
                    $"Line {i + 1} shall have a line net amount (BT-131).",
                    ValidationSeverity.Error,
                    $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount"));
            }
            else
            {
                results.Add(new ValidationResult("CII-BR-10",
                    $"Line {i + 1} net amount present: {lineTotalAmount.Value} (BT-131).",
                    ValidationSeverity.Info,
                    $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                    lineTotalAmount.Value.ToString()));
            }

            // CII-BR-CO15: Line net amount = Quantity × Price
            if (netPrice.HasValue && lineTotalAmount.HasValue && netPrice.Value > 0)
            {
                var quantity = item.SpecifiedLineTradeDelivery?.BilledQuantity?.Value ?? 0m;
                var expected = Math.Round(quantity * netPrice.Value, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(expected - lineTotalAmount.Value) > 0.02m)
                {
                    results.Add(new ValidationResult(
                        "CII-BR-CO15",
                        $"Line {i + 1} net amount ({lineTotalAmount.Value}) does not equal Quantity ({quantity}) × Price ({netPrice.Value}) = {expected}.",
                        ValidationSeverity.Warning,
                        $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                        lineTotalAmount.Value.ToString()));
                }
                else
                {
                    results.Add(new ValidationResult(
                        "CII-BR-CO15",
                        $"Line {i + 1} net amount matches Quantity × Price.",
                        ValidationSeverity.Info,
                        $"{linePath}/ram:SpecifiedLineTradeSettlement/ram:SpecifiedTradeSettlementLineMonetarySummation/ram:LineTotalAmount",
                        lineTotalAmount.Value.ToString()));
                }
            }
        }
    }

    private static FormatValidationResult BuildResult(List<ValidationResult> results)
    {
        var hasErrors = results.Any(r => r.Severity == ValidationSeverity.Error);
        return new FormatValidationResult(!hasErrors, results, InvoiceFormat.Cii);
    }

    private static T DeserializeFromStream<T>(Stream stream) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return (T)serializer.Deserialize(reader)!;
    }
}
