using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 MonetaryTotal (cac:LegalMonetaryTotal).</summary>
public class UblMonetaryTotal
{
    [XmlElement("LineExtensionAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? LineExtensionAmount { get; set; }

    [XmlElement("TaxExclusiveAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? TaxExclusiveAmount { get; set; }

    [XmlElement("TaxInclusiveAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? TaxInclusiveAmount { get; set; }

    [XmlElement("AllowanceTotalAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? AllowanceTotalAmount { get; set; }

    [XmlElement("ChargeTotalAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? ChargeTotalAmount { get; set; }

    [XmlElement("PrepaidAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? PrepaidAmount { get; set; }

    [XmlElement("PayableAmount", Namespace = UblNamespaces.Cbc)]
    public UblAmountType? PayableAmount { get; set; }
}
