using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 InvoiceTaxTotal (cac:TaxTotal).</summary>
public class UblTaxTotal
{
    [XmlElement("TaxAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? TaxAmount { get; set; }

    [XmlElement("TaxSubtotal", Namespace = UblNamespaces.Cac)]
    public List<UblTaxSubtotal> TaxSubtotals { get; set; } = new();
}

/// <summary>UBL 2.1 TaxSubtotal (cac:TaxSubtotal).</summary>
public class UblTaxSubtotal
{
    [XmlElement("TaxableAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? TaxableAmount { get; set; }

    [XmlElement("TaxAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? TaxAmount { get; set; }

    [XmlElement("TaxCategory", Namespace = UblNamespaces.Cac)]
    public UblTaxCategory? TaxCategory { get; set; }
}

/// <summary>UBL 2.1 TaxCategory (cac:TaxCategory).</summary>
public class UblTaxCategory
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }

    [XmlElement("Percent", Namespace = UblNamespaces.Cbc)]
    public decimal? Percent { get; set; }

    [XmlElement("TaxExemptionReason", Namespace = UblNamespaces.Cbc)]
    public string? TaxExemptionReason { get; set; }

    [XmlElement("TaxScheme", Namespace = UblNamespaces.Cac)]
    public UblTaxScheme? TaxScheme { get; set; }
}
