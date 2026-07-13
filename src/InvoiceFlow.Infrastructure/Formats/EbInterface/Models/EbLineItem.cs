using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

/// <summary>ebInterface line item — a single goods or services entry on the invoice.</summary>
public class EbLineItem
{
    /// <summary>Sequential line number (1-based, mandatory).</summary>
    [XmlElement("LineNumber")]
    public int LineNumber { get; set; }

    /// <summary>Description of the goods or services (mandatory).</summary>
    [XmlElement("Description")]
    public string? Description { get; set; }

    /// <summary>Quantity of goods or services with unit of measure.</summary>
    [XmlElement("Quantity")]
    public EbQuantity? Quantity { get; set; }

    /// <summary>Unit price per item (mandatory).</summary>
    [XmlElement("UnitPrice")]
    public decimal UnitPrice { get; set; }

    /// <summary>Total amount for this line (Quantity * UnitPrice, mandatory).</summary>
    [XmlElement("LineTotalAmount")]
    public decimal LineTotalAmount { get; set; }

    /// <summary>Tax information for this line item — tax rate and per-tax breakdown.</summary>
    [XmlElement("Tax")]
    public EbLineTax? Tax { get; set; }

    /// <summary>Vendor's product or item code (optional).</summary>
    [XmlElement("SellerOrderReference")]
    public string? SellerOrderReference { get; set; }

    /// <summary>Buyer's order reference (optional).</summary>
    [XmlElement("BuyerOrderReference")]
    public string? BuyerOrderReference { get; set; }

    /// <summary>Delivery note reference (optional).</summary>
    [XmlElement("DeliveryNoteReference")]
    public string? DeliveryNoteReference { get; set; }
}

/// <summary>ebInterface quantity type — decimal value with a unit-of-measure attribute.</summary>
public class EbQuantity
{
    /// <summary>The numeric quantity value.</summary>
    [XmlText]
    public decimal Value { get; set; }

    /// <summary>UN/ECE Rec. 20 unit code (e.g., EA, STK, H, KGM).</summary>
    [XmlAttribute("unit")]
    public string? Unit { get; set; }
}

/// <summary>ebInterface line-level tax — wraps a tax rate and a list of individual tax entries.</summary>
public class EbLineTax
{
    /// <summary>Tax rate applied to this line (percentage, e.g., 20.00 for 20%).</summary>
    [XmlElement("TaxRate")]
    public decimal TaxRate { get; set; }

    /// <summary>List of individual tax amounts broken down by type (VAT, etc.).</summary>
    [XmlArray("Taxes")]
    [XmlArrayItem("Tax")]
    public List<EbTaxItem> Taxes { get; set; } = new();
}

/// <summary>ebInterface tax item — a single tax amount and its type code.
/// Used at both document level (direct child of Invoice) and line level (within Taxes list).</summary>
public class EbTaxItem
{
    /// <summary>Tax amount in invoice currency.</summary>
    [XmlElement("Amount")]
    public decimal Amount { get; set; }

    /// <summary>Tax type code (e.g., VAT for value-added tax).</summary>
    [XmlElement("TypeCode")]
    public string? TypeCode { get; set; }
}
