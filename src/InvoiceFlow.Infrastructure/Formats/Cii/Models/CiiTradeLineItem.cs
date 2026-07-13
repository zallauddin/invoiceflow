using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>CII SupplyChainTradeLineItem — a single line item in the invoice.</summary>
public class CiiTradeLineItem
{
    /// <summary>Associated document line document (line ID, notes).</summary>
    [XmlElement("AssociatedDocumentLineDocument", Namespace = CiiNamespaces.Ram)]
    public CiiDocumentLineDocument? AssociatedDocumentLineDocument { get; set; }

    /// <summary>Specified trade product (description, seller item ID, etc.).</summary>
    [XmlElement("SpecifiedTradeProduct", Namespace = CiiNamespaces.Ram)]
    public CiiTradeProduct? SpecifiedTradeProduct { get; set; }

    /// <summary>Specified line trade delivery (quantity and unit).</summary>
    [XmlElement("SpecifiedLineTradeDelivery", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeDelivery? SpecifiedLineTradeDelivery { get; set; }

    /// <summary>Specified line trade settlement (price, line total, tax).</summary>
    [XmlElement("SpecifiedLineTradeAgreement", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeAgreement? SpecifiedLineTradeAgreement { get; set; }

    /// <summary>Specified line trade settlement (line settlement details).</summary>
    [XmlElement("SpecifiedLineTradeSettlement", Namespace = CiiNamespaces.Ram)]
    public CiiLineTradeSettlement? SpecifiedLineTradeSettlement { get; set; }
}

/// <summary>CII DocumentLineDocument — document-level information for a line item.</summary>
public class CiiDocumentLineDocument
{
    /// <summary>Line identifier / sequential line number (BT-126).</summary>
    [XmlElement("LineID", Namespace = CiiNamespaces.Ram)]
    public string? LineId { get; set; }

    /// <summary>Included notes on this line.</summary>
    [XmlElement("IncludedNote", Namespace = CiiNamespaces.Ram)]
    public List<CiiNote> IncludedNotes { get; set; } = new();
}

/// <summary>CII TradeProduct — product details for a line item.</summary>
public class CiiTradeProduct
{
    /// <summary>Product name.</summary>
    [XmlElement("Name", Namespace = CiiNamespaces.Ram)]
    public string? Name { get; set; }

    /// <summary>Product description.</summary>
    [XmlElement("Description", Namespace = CiiNamespaces.Ram)]
    public string? Description { get; set; }

    /// <summary>Seller's item identifier (BT-155).</summary>
    [XmlElement("SellersItemIdentification", Namespace = CiiNamespaces.Ram)]
    public CiiItemIdentification? SellersItemIdentification { get; set; }

    /// <summary>Buyer's item identifier (BT-156).</summary>
    [XmlElement("BuyersItemIdentification", Namespace = CiiNamespaces.Ram)]
    public CiiItemIdentification? BuyersItemIdentification { get; set; }

    /// <summary>Global identifier (GTIN, BT-157).</summary>
    [XmlElement("GlobalID", Namespace = CiiNamespaces.Ram)]
    public CiiIdentifier? GlobalId { get; set; }

    /// <summary>Classified trade tax category (BT-151/152/153).</summary>
    [XmlElement("ClassifiedTradeTax", Namespace = CiiNamespaces.Ram)]
    public CiiTradeTax? ClassifiedTradeTax { get; set; }
}

/// <summary>CII ItemIdentification — an item identifier (seller's, buyer's, etc.).</summary>
public class CiiItemIdentification
{
    /// <summary>The item identifier value.</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }
}

/// <summary>CII LineTradeDelivery — delivery details for a line item (quantity, unit).</summary>
public class CiiLineTradeDelivery
{
    /// <summary>Billed quantity (BT-129).</summary>
    [XmlElement("BilledQuantity", Namespace = CiiNamespaces.Ram)]
    public CiiQuantityType? BilledQuantity { get; set; }
}

/// <summary>CII QuantityType — a quantity with unit code attribute.</summary>
public class CiiQuantityType
{
    /// <summary>UN/ECE Rec. 20 unit code (e.g., EA, H, KGM, LTR).</summary>
    [XmlAttribute("unitCode")]
    public string? UnitCode { get; set; }

    /// <summary>The quantity value.</summary>
    [XmlText]
    public decimal Value { get; set; }
}

/// <summary>CII LineTradeAgreement — pricing agreement for a line item.</summary>
public class CiiLineTradeAgreement
{
    /// <summary>Net price (BT-146).</summary>
    [XmlElement("NetPriceProductTradePrice", Namespace = CiiNamespaces.Ram)]
    public CiiTradePrice? NetPriceProductTradePrice { get; set; }
}

/// <summary>CII TradePrice — a trade price with optional basis amount.</summary>
public class CiiTradePrice
{
    /// <summary>The price amount.</summary>
    [XmlElement("ChargeAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? ChargeAmount { get; set; }

    /// <summary>The basis quantity for the price (optional).</summary>
    [XmlElement("BasisQuantity", Namespace = CiiNamespaces.Ram)]
    public CiiQuantityType? BasisQuantity { get; set; }
}

/// <summary>CII LineTradeSettlement — settlement details for a line item (line total, tax).</summary>
public class CiiLineTradeSettlement
{
    /// <summary>Included trade tax for this line.</summary>
    [XmlElement("ApplicableTradeTax", Namespace = CiiNamespaces.Ram)]
    public CiiTradeTax? ApplicableTradeTax { get; set; }

    /// <summary>Specified trade settlement line monetary summation (line total).</summary>
    [XmlElement("SpecifiedTradeSettlementLineMonetarySummation", Namespace = CiiNamespaces.Ram)]
    public CiiLineMonetarySummation? SpecifiedTradeSettlementLineMonetarySummation { get; set; }
}

/// <summary>CII LineMonetarySummation — monetary summation for a line item.</summary>
public class CiiLineMonetarySummation
{
    /// <summary>Line total amount (BT-131).</summary>
    [XmlElement("LineTotalAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? LineTotalAmount { get; set; }
}

/// <summary>CII AmountType — a monetary amount with optional currency ID attribute.</summary>
public class CiiAmountType
{
    /// <summary>ISO 4217 currency code (e.g., EUR, USD).</summary>
    [XmlAttribute("currencyID")]
    public string? CurrencyId { get; set; }

    /// <summary>The monetary amount value.</summary>
    [XmlText]
    public decimal Value { get; set; }
}
