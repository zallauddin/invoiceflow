using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

/// <summary>ebInterface party — represents either the Biller (supplier) or InvoiceRecipient (buyer).
/// Contains identification, name, address, and optional contact information.</summary>
public class EbParty
{
    /// <summary>Austrian VAT identification number (Umsatzsteuer-Identifikationsnummer, UID).
    /// Format: ATU followed by 8 digits (e.g., ATU12345678).</summary>
    [XmlElement("VATIdentificationNumber")]
    public string? VATIdentificationNumber { get; set; }

    /// <summary>Company or person name (mandatory).</summary>
    [XmlElement("Name")]
    public string? Name { get; set; }

    /// <summary>Trade name / business name (optional, for differs-from-legal-name scenarios).</summary>
    [XmlElement("TradeName")]
    public string? TradeName { get; set; }

    /// <summary>Postal address of the party.</summary>
    [XmlElement("Address")]
    public EbAddress? Address { get; set; }

    /// <summary>Contact information (optional).</summary>
    [XmlElement("Contact")]
    public EbContact? Contact { get; set; }

    /// <summary>Electronic address identifier (e.g., email, GLN).</summary>
    [XmlElement("ElectronicAddress")]
    public string? ElectronicAddress { get; set; }
}

/// <summary>ebInterface postal address — street, town, ZIP, and country code.</summary>
public class EbAddress
{
    /// <summary>Street name and house number.</summary>
    [XmlElement("Street")]
    public string? Street { get; set; }

    /// <summary>Town or city name.</summary>
    [XmlElement("Town")]
    public string? Town { get; set; }

    /// <summary>Postal / ZIP code.</summary>
    [XmlElement("ZIP")]
    public string? ZIP { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g., AT for Austria).</summary>
    [XmlElement("Country")]
    public string? Country { get; set; }
}

/// <summary>ebInterface contact information — phone, email, and contact person name.</summary>
public class EbContact
{
    /// <summary>Contact person name.</summary>
    [XmlElement("Name")]
    public string? Name { get; set; }

    /// <summary>Phone number.</summary>
    [XmlElement("Phone")]
    public string? Phone { get; set; }

    /// <summary>Email address.</summary>
    [XmlElement("Email")]
    public string? Email { get; set; }

    /// <summary>Facsimile / fax number (optional).</summary>
    [XmlElement("Fax")]
    public string? Fax { get; set; }
}
