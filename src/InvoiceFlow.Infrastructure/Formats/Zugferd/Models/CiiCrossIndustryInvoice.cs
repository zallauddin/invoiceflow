using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

// ─────────────────────────────────────────────────────────────
// Root document
// ─────────────────────────────────────────────────────────────

/// <summary>
/// CII CrossIndustryInvoice root element (rsm:CrossIndustryInvoice).
/// The top-level container for a ZUGFeRD/Factur-X invoice document.
/// </summary>
[XmlRoot("CrossIndustryInvoice", Namespace = CiiNamespaces.Rsm)]
public class CiiCrossIndustryInvoice
{
    /// <summary>Document context — contains the profile/guideline ID.</summary>
    [XmlElement("ExchangedDocumentContext", Namespace = CiiNamespaces.Rsm)]
    public CiiExchangedDocumentContext? ExchangedDocumentContext { get; set; }

    /// <summary>Exchanged document — invoice number, type code, issue date, notes.</summary>
    [XmlElement("ExchangedDocument", Namespace = CiiNamespaces.Rsm)]
    public CiiExchangedDocument? ExchangedDocument { get; set; }

    /// <summary>Supply chain trade transaction — the business content.</summary>
    [XmlElement("SupplyChainTradeTransaction", Namespace = CiiNamespaces.Rsm)]
    public CiiSupplyChainTradeTransaction? SupplyChainTradeTransaction { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Document context
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ExchangedDocumentContext (rsm) — identifies the guideline/profile used.
/// </summary>
public class CiiExchangedDocumentContext
{
    /// <summary>Guideline profile identifier (e.g., EN 16931 compliant URI).</summary>
    [XmlElement("GuidelineSpecifiedDocumentContextParameter", Namespace = CiiNamespaces.Ram)]
    public CiiDocumentContextParameter? GuidelineSpecifiedDocumentContextParameter { get; set; }
}

/// <summary>A document context parameter with an ID element.</summary>
public class CiiDocumentContextParameter
{
    /// <summary>The guideline or profile identifier URI.</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Exchanged document
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ExchangedDocument (rsm) — header-level document metadata.
/// </summary>
public class CiiExchangedDocument
{
    /// <summary>Invoice number (BT-1).</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }

    /// <summary>Invoice type code — 380 for invoice, 381 for credit note (BT-3).</summary>
    [XmlElement("TypeCode", Namespace = CiiNamespaces.Ram)]
    public string? TypeCode { get; set; }

    /// <summary>Issue date of the invoice (BT-2).</summary>
    [XmlElement("IssueDateTime", Namespace = CiiNamespaces.Ram)]
    public CiiDateTimeType? IssueDateTime { get; set; }

    /// <summary>Included notes (free text, purpose codes, etc.).</summary>
    [XmlElement("IncludedNote", Namespace = CiiNamespaces.Ram)]
    public List<CiiNote> IncludedNotes { get; set; } = new();
}

/// <summary>A note attached to the exchanged document.</summary>
public class CiiNote
{
    /// <summary>Note purpose code (e.g., "916" for general note, "ADU" for additional info).</summary>
    [XmlElement("ContentLineCode", Namespace = CiiNamespaces.Ram)]
    public string? ContentLineCode { get; set; }

    /// <summary>Note text content.</summary>
    [XmlElement("Content", Namespace = CiiNamespaces.Ram)]
    public string? Content { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Supply chain trade transaction
// ─────────────────────────────────────────────────────────────

/// <summary>
/// SupplyChainTradeTransaction (rsm) — groups header trade agreement, delivery, settlement, and line items.
/// </summary>
public class CiiSupplyChainTradeTransaction
{
    /// <summary>Header trade agreement — seller, buyer, payment terms, references.</summary>
    [XmlElement("ApplicableHeaderTradeAgreement", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeAgreement? ApplicableHeaderTradeAgreement { get; set; }

    /// <summary>Header trade delivery — delivery location.</summary>
    [XmlElement("ApplicableHeaderTradeDelivery", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeDelivery? ApplicableHeaderTradeDelivery { get; set; }

    /// <summary>Header trade settlement — currency, tax, monetary summation.</summary>
    [XmlElement("ApplicableHeaderTradeSettlement", Namespace = CiiNamespaces.Ram)]
    public CiiHeaderTradeSettlement? ApplicableHeaderTradeSettlement { get; set; }

    /// <summary>Included line items.</summary>
    [XmlElement("IncludedSupplyChainTradeLineItem", Namespace = CiiNamespaces.Ram)]
    public List<CiiTradeLineItem> IncludedSupplyChainTradeLineItems { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
// Header trade agreement
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ApplicableHeaderTradeAgreement (ram) — seller, buyer, payment terms, and buyer reference.
/// </summary>
public class CiiHeaderTradeAgreement
{
    /// <summary>Buyer reference / PO number (BT-13).</summary>
    [XmlElement("BuyerReference", Namespace = CiiNamespaces.Ram)]
    public string? BuyerReference { get; set; }

    /// <summary>Seller trade party (BT-27+).</summary>
    [XmlElement("SellerTradeParty", Namespace = CiiNamespaces.Ram)]
    public CiiTradeParty? SellerTradeParty { get; set; }

    /// <summary>Buyer trade party (BT-44+).</summary>
    [XmlElement("BuyerTradeParty", Namespace = CiiNamespaces.Ram)]
    public CiiTradeParty? BuyerTradeParty { get; set; }

    /// <summary>Applicable payment terms (due date, etc.).</summary>
    [XmlElement("ApplicablePaymentTerms", Namespace = CiiNamespaces.Ram)]
    public CiiPaymentTerms? ApplicablePaymentTerms { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Header trade delivery
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ApplicableHeaderTradeDelivery (ram) — delivery information.
/// </summary>
public class CiiHeaderTradeDelivery
{
    /// <summary>Ship-to location.</summary>
    [XmlElement("ShipToEventTradeLocation", Namespace = CiiNamespaces.Ram)]
    public CiiTradeLocation? ShipToEventTradeLocation { get; set; }
}

/// <summary>A trade location (name-based).</summary>
public class CiiTradeLocation
{
    /// <summary>Location name.</summary>
    [XmlElement("Name", Namespace = CiiNamespaces.Ram)]
    public string? Name { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Header trade settlement
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ApplicableHeaderTradeSettlement (ram) — currency, tax breakdown, and monetary summation.
/// </summary>
public class CiiHeaderTradeSettlement
{
    /// <summary>Invoice currency code (BT-5, ISO 4217).</summary>
    [XmlElement("InvoiceCurrencyCode", Namespace = CiiNamespaces.Ram)]
    public string? InvoiceCurrencyCode { get; set; }

    /// <summary>Applicable trade tax entries — one per tax rate.</summary>
    [XmlElement("ApplicableTradeTax", Namespace = CiiNamespaces.Ram)]
    public List<CiiApplicableTradeTax> ApplicableTradeTaxes { get; set; } = new();

    /// <summary>Header-level monetary summation (totals).</summary>
    [XmlElement("SpecifiedTradeSettlementHeaderMonetarySummation", Namespace = CiiNamespaces.Ram)]
    public CiiMonetarySummation? SpecifiedTradeSettlementHeaderMonetarySummation { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Payment terms
// ─────────────────────────────────────────────────────────────

/// <summary>
/// ApplicablePaymentTerms (ram) — payment terms with optional due date.
/// </summary>
public class CiiPaymentTerms
{
    /// <summary>Payment due date (BT-9).</summary>
    [XmlElement("DueDateDateTime", Namespace = CiiNamespaces.Ram)]
    public CiiDateTimeType? DueDateDateTime { get; set; }
}
