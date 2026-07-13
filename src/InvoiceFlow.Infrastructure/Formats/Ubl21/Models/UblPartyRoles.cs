using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 AccountingSupplierParty (cac:AccountingSupplierParty).</summary>
public class UblSupplierParty
{
    [XmlElement("Party", Namespace = UblNamespaces.Cac)]
    public UblParty? Party { get; set; }
}

/// <summary>UBL 2.1 AccountingCustomerParty (cac:AccountingCustomerParty).</summary>
public class UblCustomerParty
{
    [XmlElement("Party", Namespace = UblNamespaces.Cac)]
    public UblParty? Party { get; set; }
}

/// <summary>UBL 2.1 PayeeParty (cac:PayeeParty).</summary>
public class UblPayeeParty
{
    [XmlElement("Party", Namespace = UblNamespaces.Cac)]
    public UblParty? Party { get; set; }
}
