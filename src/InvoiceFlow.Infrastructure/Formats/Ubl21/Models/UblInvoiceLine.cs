using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 InvoiceLine (cac:InvoiceLine).</summary>
public class UblInvoiceLine
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }

    [XmlElement("InvoicedQuantity", Namespace = UblNamespaces.Cbc)]
    public UblMeasureType? InvoicedQuantity { get; set; }

    [XmlElement("LineExtensionAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? LineExtensionAmount { get; set; }

    [XmlElement("Item", Namespace = UblNamespaces.Cac)]
    public UblItem? Item { get; set; }

    [XmlElement("Price", Namespace = UblNamespaces.Cac)]
    public UblPrice? Price { get; set; }
}

/// <summary>UBL 2.1 Item (cac:Item).</summary>
public class UblItem
{
    [XmlElement("Description", Namespace = UblNamespaces.Cbc)]
    public string? Description { get; set; }

    [XmlElement("Name", Namespace = UblNamespaces.Cbc)]
    public string? Name { get; set; }

    [XmlElement("SellersItemIdentification", Namespace = UblNamespaces.Cac)]
    public UblItemIdentification? SellersItemIdentification { get; set; }

    [XmlElement("ClassifiedTaxCategory", Namespace = UblNamespaces.Cac)]
    public UblTaxCategory? ClassifiedTaxCategory { get; set; }
}

/// <summary>UBL 2.1 ItemIdentification (cac:SellersItemIdentification).</summary>
public class UblItemIdentification
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }
}

/// <summary>UBL 2.1 Price (cac:Price).</summary>
public class UblPrice
{
    [XmlElement("PriceAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? PriceAmount { get; set; }
}
