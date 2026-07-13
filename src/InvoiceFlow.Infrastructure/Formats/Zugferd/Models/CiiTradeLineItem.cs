using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

// ─────────────────────────────────────────────────────────────
// Trade line item
// ─────────────────────────────────────────────────────────────

/// <summary>
/// CII IncludedSupplyChainTradeLineItem (ram) — a single invoice line.
/// Contains line document reference, product info, price, quantity, and settlement.
/// </summary>
public class CiiTradeLineItem
{
    /// <summary>Line document reference — line ID number.</summary>
    [XmlElement("AssociatedDocumentLineDocument", Namespace = CiiNamespaces.Ram)]
    public CiiDocumentLineDocument? AssociatedDocumentLineDocument { get; set; }

    /// <summary>Trade product information — name, seller ID, description.</summary>
    [XmlElement("SpecifiedTradeProduct", Namespace = CiiNamespaces.Ram)]
    public CiiTradeProduct? SpecifiedTradeProduct { get; set; }

    /// <summary>Line-level trade agreement — net price.</summary>
    [XmlElement("SpecifiedLineTradeAgreement", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeAgreement? SpecifiedLineTradeAgreement { get; set; }

    /// <summary>Line-level trade delivery — billed quantity.</summary>
    [XmlElement("SpecifiedLineTradeDelivery", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeDelivery? SpecifiedLineTradeDelivery { get; set; }

    /// <summary>Line-level trade settlement — tax and line total amount.</summary>
    [XmlElement("SpecifiedLineTradeSettlement", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeSettlement? SpecifiedLineTradeSettlement { get; set; }
}

/// <summary>
/// CII AssociatedDocumentLineDocument — line number reference.
/// </summary>
public class CiiDocumentLineDocument
{
    /// <summary>Sequential line number (BT-126).</summary>
    [XmlElement("LineID", Namespace = CiiNamespaces.Ram)]
    public string? LineId { get; set; }
}

/// <summary>
/// CII SpecifiedTradeProduct — product identification and description.
/// </summary>
public class CiiTradeProduct
{
    /// <summary>Product name (BT-153).</summary>
    [XmlElement("Name", Namespace = CiiNamespaces.Ram)]
    public string? Name { get; set; }

    /// <summary>Seller-assigned product ID (BT-155).</summary>
    [XmlElement("SellerAssignedID", Namespace = CiiNamespaces.Ram)]
    public string? SellerAssignedId { get; set; }

    /// <summary>Product description (BT-154).</summary>
    [XmlElement("Description", Namespace = CiiNamespaces.Ram)]
    public string? Description { get; set; }
}

/// <summary>
/// CII SpecifiedLineTradeAgreement — line-level price information.
/// </summary>
public class CiiLineTradeAgreement
{
    /// <summary>Net price product trade price (BT-146).</summary>
    [XmlElement("NetPriceProductTradePrice", Namespace = CiiNamespaces.Ram)]
    public CiiProductTradePrice? NetPriceProductTradePrice { get; set; }
}

/// <summary>
/// CII ProductTradePrice — a price with optional currency attribute.
/// </summary>
public class CiiProductTradePrice
{
    /// <summary>Charge amount (unit price, BT-146).</summary>
    [XmlElement("ChargeAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? ChargeAmount { get; set; }
}

/// <summary>
/// CII SpecifiedLineTradeDelivery — line quantity.
/// </summary>
public class CiiLineTradeDelivery
{
    /// <summary>Billed quantity (BT-129) with unit code (BT-130).</summary>
    [XmlElement("BilledQuantity", Namespace = CiiNamespaces.Ram)]
    public CiiQuantityType? BilledQuantity { get; set; }
}

/// <summary>
/// CII SpecifiedLineTradeSettlement — line tax and line total.
/// </summary>
public class CiiLineTradeSettlement
{
    /// <summary>Applicable trade tax for this line.</summary>
    [XmlElement("ApplicableTradeTax", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeTax? ApplicableTradeTax { get; set; }

    /// <summary>Line monetary summation — net line total amount.</summary>
    [XmlElement("SpecifiedTradeSettlementLineMonetarySummation", Namespace = CiiNamespaces.Ram)]
    public CiiLineMonetarySummation? SpecifiedTradeSettlementLineMonetarySummation { get; set; }
}

/// <summary>
/// CII line-level trade tax — type code, category code, and applicable percent.
/// </summary>
public class CiiLineTradeTax
{
    /// <summary>Tax type code (e.g., "VAT").</summary>
    [XmlElement("TypeCode", Namespace = CiiNamespaces.Ram)]
    public string? TypeCode { get; set; }

    /// <summary>Tax category code (S, Z, E, etc.).</summary>
    [XmlElement("CategoryCode", Namespace = CiiNamespaces.Ram)]
    public string? CategoryCode { get; set; }

    /// <summary>Tax rate percentage.</summary>
    [XmlElement("ApplicablePercent", Namespace = CiiNamespaces.Ram)]
    public CiiPercentType? ApplicablePercent { get; set; }
}

/// <summary>
/// CII SpecifiedTradeSettlementLineMonetarySummation — line total amount.
/// </summary>
public class CiiLineMonetarySummation
{
    /// <summary>Line total amount (BT-131) = Quantity × UnitPrice.</summary>
    [XmlElement("LineTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? LineTotalAmount { get; set; }
}
