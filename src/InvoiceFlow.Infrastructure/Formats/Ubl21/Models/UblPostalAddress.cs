using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 PostalAddress (cac:PostalAddress).</summary>
public class UblPostalAddress
{
    [XmlElement("StreetName", Namespace = UblNamespaces.Cbc)]
    public string? StreetName { get; set; }

    [XmlElement("AdditionalStreetName", Namespace = UblNamespaces.Cbc)]
    public string? AdditionalStreetName { get; set; }

    [XmlElement("CityName", Namespace = UblNamespaces.Cbc)]
    public string? CityName { get; set; }

    [XmlElement("PostalZone", Namespace = UblNamespaces.Cbc)]
    public string? PostalZone { get; set; }

    [XmlElement("CountrySubentity", Namespace = UblNamespaces.Cbc)]
    public string? CountrySubentity { get; set; }

    [XmlElement("Country", Namespace = UblNamespaces.Cac)]
    public UblCountry? Country { get; set; }
}

/// <summary>UBL 2.1 Country (cac:Country).</summary>
public class UblCountry
{
    [XmlElement("IdentificationCode", Namespace = UblNamespaces.Cbc)]
    public string? IdentificationCode { get; set; }
}
