using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 PartyName (cac:PartyName).</summary>
public class UblPartyName
{
    [XmlElement("Name", Namespace = UblNamespaces.Cbc)]
    public string? Name { get; set; }
}

/// <summary>UBL 2.1 PartyTaxScheme (cac:PartyTaxScheme).</summary>
public class UblPartyTaxScheme
{
    [XmlElement("CompanyID", Namespace = UblNamespaces.Cbc)]
    public string? CompanyId { get; set; }

    [XmlElement("TaxScheme", Namespace = UblNamespaces.Cac)]
    public UblTaxScheme? TaxScheme { get; set; }
}

/// <summary>UBL 2.1 TaxScheme (cac:TaxScheme).</summary>
public class UblTaxScheme
{
    [XmlElement("ID", Namespace = UblNamespaces.Cbc)]
    public string? Id { get; set; }
}

/// <summary>UBL 2.1 Party Legal Entity (cac:PartyLegalEntity).</summary>
public class UblPartyLegalEntity
{
    [XmlElement("RegistrationName", Namespace = UblNamespaces.Cbc)]
    public string? RegistrationName { get; set; }

    [XmlElement("CompanyID", Namespace = UblNamespaces.Cbc)]
    public string? CompanyId { get; set; }

    [XmlElement("RegistrationAddress", Namespace = UblNamespaces.Cac)]
    public UblPostalAddress? RegistrationAddress { get; set; }
}

/// <summary>UBL 2.1 Contact (cac:Contact).</summary>
public class UblContact
{
    [XmlElement("Telephone", Namespace = UblNamespaces.Cbc)]
    public string? Telephone { get; set; }

    [XmlElement("ElectronicMail", Namespace = UblNamespaces.Cbc)]
    public string? ElectronicMail { get; set; }
}

/// <summary>UBL 2.1 Party (cac:Party).</summary>
public class UblParty
{
    [XmlElement("EndpointID", Namespace = UblNamespaces.Cbc)]
    public string? EndpointId { get; set; }

    [XmlElement("PartyName", Namespace = UblNamespaces.Cac)]
    public UblPartyName? PartyName { get; set; }

    [XmlElement("PostalAddress", Namespace = UblNamespaces.Cac)]
    public UblPostalAddress? PostalAddress { get; set; }

    [XmlElement("PartyTaxScheme", Namespace = UblNamespaces.Cac)]
    public UblPartyTaxScheme? PartyTaxScheme { get; set; }

    [XmlElement("PartyLegalEntity", Namespace = UblNamespaces.Cac)]
    public UblPartyLegalEntity? PartyLegalEntity { get; set; }

    [XmlElement("Contact", Namespace = UblNamespaces.Cac)]
    public UblContact? Contact { get; set; }
}
