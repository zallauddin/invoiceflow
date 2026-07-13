using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>CII TradeParty — a seller, buyer, or other party in the trade transaction.</summary>
public class CiiTradeParty
{
    /// <summary>Party name (BT-27 for seller, BT-44 for buyer).</summary>
    [XmlElement("Name", Namespace = CiiNamespaces.Ram)]
    public string? Name { get; set; }

    /// <summary>Defined trade contact (email, phone).</summary>
    [XmlElement("DefinedTradeContact", Namespace = CiiNamespaces.Ram)]
    public CiiTradeContact? DefinedTradeContact { get; set; }

    /// <summary>Postal trade address.</summary>
    [XmlElement("PostalTradeAddress", Namespace = CiiNamespaces.Ram)]
    public CiiPostalTradeAddress? PostalTradeAddress { get; set; }

    /// <summary>Specified legal organization (registration name, company ID).</summary>
    [XmlElement("SpecifiedLegalOrganization", Namespace = CiiNamespaces.Ram)]
    public CiiLegalOrganization? SpecifiedLegalOrganization { get; set; }

    /// <summary>Specified tax registrations (tax IDs).</summary>
    [XmlElement("SpecifiedTaxRegistration", Namespace = CiiNamespaces.Ram)]
    public List<CiiTaxRegistration> SpecifiedTaxRegistrations { get; set; } = new();
}

/// <summary>CII TradeContact — contact person details for a trade party.</summary>
public class CiiTradeContact
{
    /// <summary>Person name.</summary>
    [XmlElement("PersonName", Namespace = CiiNamespaces.Ram)]
    public string? PersonName { get; set; }

    /// <summary>Telephone number.</summary>
    [XmlElement("TelephoneUniversalCommunication", Namespace = CiiNamespaces.Ram)]
    public CiiUniversalCommunication? TelephoneUniversalCommunication { get; set; }

    /// <summary>Email address.</summary>
    [XmlElement("EmailURIUniversalCommunication", Namespace = CiiNamespaces.Ram)]
    public CiiUniversalCommunication? EmailUriUniversalCommunication { get; set; }
}

/// <summary>CII UniversalCommunication — telephone or email communication channel.</summary>
public class CiiUniversalCommunication
{
    /// <summary>The URI ID (phone number or email address).</summary>
    [XmlElement("URIID", Namespace = CiiNamespaces.Ram)]
    public string? UriId { get; set; }
}

/// <summary>CII PostalTradeAddress — postal address of a trade party.</summary>
public class CiiPostalTradeAddress
{
    /// <summary>Postcode / postal zone (BT-38/53).</summary>
    [XmlElement("PostcodeCode", Namespace = CiiNamespaces.Ram)]
    public string? PostcodeCode { get; set; }

    /// <summary>Street name (BT-35/50).</summary>
    [XmlElement("LineOne", Namespace = CiiNamespaces.Ram)]
    public string? LineOne { get; set; }

    /// <summary>Additional street / address line 2.</summary>
    [XmlElement("LineTwo", Namespace = CiiNamespaces.Ram)]
    public string? LineTwo { get; set; }

    /// <summary>City name (BT-37/52).</summary>
    [XmlElement("CityName", Namespace = CiiNamespaces.Ram)]
    public string? CityName { get; set; }

    /// <summary>Country subdivision / state (BT-39/54).</summary>
    [XmlElement("CountrySubdivisionName", Namespace = CiiNamespaces.Ram)]
    public string? CountrySubdivisionName { get; set; }

    /// <summary>Country ID (ISO 3166-1 alpha-2, BT-40/55).</summary>
    [XmlElement("CountryID", Namespace = CiiNamespaces.Ram)]
    public string? CountryId { get; set; }
}

/// <summary>CII LegalOrganization — legal entity information for a trade party.</summary>
public class CiiLegalOrganization
{
    /// <summary>Trading name (trading business name).</summary>
    [XmlElement("TradingBusinessName", Namespace = CiiNamespaces.Ram)]
    public string? TradingBusinessName { get; set; }

    /// <summary>Legal registration identifier (company number).</summary>
    [XmlElement("LegalRegistrationIdentifier", Namespace = CiiNamespaces.Ram)]
    public CiiIdentifier? LegalRegistrationIdentifier { get; set; }

    /// <summary>Legal organization identifier (BT-47/57).</summary>
    [XmlElement("Identifier", Namespace = CiiNamespaces.Ram)]
    public CiiIdentifier? Identifier { get; set; }
}

/// <summary>CII Identifier — an identifier with optional scheme identifier.</summary>
public class CiiIdentifier
{
    /// <summary>The identifier value.</summary>
    [XmlAttribute("schemeID")]
    public string? SchemeId { get; set; }

    /// <summary>The identifier value as text content.</summary>
    [XmlText]
    public string Value { get; set; } = string.Empty;
}

/// <summary>CII TaxRegistration — a tax registration identifier (e.g., VAT ID).</summary>
public class CiiTaxRegistration
{
    /// <summary>Tax registration identifier (e.g., DE123456789 for VAT).</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public CiiTaxRegistrationId? Id { get; set; }
}

/// <summary>CII TaxRegistrationId — tax ID with an optional type scheme.</summary>
public class CiiTaxRegistrationId
{
    /// <summary>Tax type scheme (e.g., VA = VAT).</summary>
    [XmlAttribute("schemeID")]
    public string? SchemeId { get; set; }

    /// <summary>The tax registration number.</summary>
    [XmlText]
    public string Value { get; set; } = string.Empty;
}
