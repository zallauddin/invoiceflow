using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

// ─────────────────────────────────────────────────────────────
// Trade party
// ─────────────────────────────────────────────────────────────

/// <summary>
/// CII TradeParty (ram) — represents a seller, buyer, or other party.
/// Contains name, postal address, tax registration, and legal organization.
/// </summary>
public class CiiTradeParty
{
    /// <summary>Party name (BT-27 for seller, BT-44 for buyer).</summary>
    [XmlElement("Name", Namespace = CiiNamespaces.Ram)]
    public string? Name { get; set; }

    /// <summary>Defined trade contact — email, phone, etc.</summary>
    [XmlElement("DefinedTradeContact", Namespace = CiiNamespaces.Ram)]
    public CiiTradeContact? DefinedTradeContact { get; set; }

    /// <summary>Postal trade address.</summary>
    [XmlElement("PostalTradeAddress", Namespace = CiiNamespaces.Ram)]
    public CiiPostalTradeAddress? PostalTradeAddress { get; set; }

    /// <summary>Tax registrations (VAT ID, etc.).</summary>
    [XmlElement("SpecifiedTaxRegistration", Namespace = CiiNamespaces.Ram)]
    public List<CiiTaxRegistration> SpecifiedTaxRegistrations { get; set; } = new();

    /// <summary>Legal organization details — trading business name, legal entity ID.</summary>
    [XmlElement("SpecifiedLegalOrganization", Namespace = CiiNamespaces.Ram)]
    public CiiLegalOrganization? SpecifiedLegalOrganization { get; set; }
}

/// <summary>
/// CII trade contact — email or phone contact for a party.
/// </summary>
public class CiiTradeContact
{
    /// <summary>Email address via URIUniversalCommunication.</summary>
    [XmlElement("URIUniversalCommunication", Namespace = CiiNamespaces.Ram)]
    public CiiUriUniversalCommunication? URIUniversalCommunication { get; set; }
}

/// <summary>
/// URI-based universal communication — holds email, phone, or other URI.
/// </summary>
public class CiiUriUniversalCommunication
{
    /// <summary>The URI identifier (e.g., mailto:... for email).</summary>
    [XmlElement("URIID", Namespace = CiiNamespaces.Ram)]
    public string? UriId { get; set; }
}

/// <summary>
/// CII PostalTradeAddress — postal address of a trade party.
/// </summary>
public class CiiPostalTradeAddress
{
    /// <summary>Postcode (BT-53).</summary>
    [XmlElement("PostcodeCode", Namespace = CiiNamespaces.Ram)]
    public string? PostcodeCode { get; set; }

    /// <summary>City name (BT-52).</summary>
    [XmlElement("CityName", Namespace = CiiNamespaces.Ram)]
    public string? CityName { get; set; }

    /// <summary>Country code (BT-55, ISO 3166-1 alpha-2).</summary>
    [XmlElement("CountryID", Namespace = CiiNamespaces.Ram)]
    public string? CountryId { get; set; }
}

/// <summary>
/// CII tax registration — holds a tax ID (e.g., VAT registration number).
/// </summary>
public class CiiTaxRegistration
{
    /// <summary>Tax registration identifier (e.g., "DE123456789").</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }
}

/// <summary>
/// CII SpecifiedLegalOrganization — legal entity details.
/// </summary>
public class CiiLegalOrganization
{
    /// <summary>Trading business name (BT-27/44 human-readable).</summary>
    [XmlElement("TradingBusinessName", Namespace = CiiNamespaces.Ram)]
    public string? TradingBusinessName { get; set; }

    /// <summary>Legal entity identifier (LEI or other).</summary>
    [XmlElement("LegalEntityIdentifier", Namespace = CiiNamespaces.Ram)]
    public CiiLegalEntityIdentifier? LegalEntityIdentifier { get; set; }
}

/// <summary>
/// CII LegalEntityIdentifier — globally unique legal entity ID.
/// </summary>
public class CiiLegalEntityIdentifier
{
    /// <summary>The identifier value.</summary>
    [XmlElement("ID", Namespace = CiiNamespaces.Ram)]
    public string? Id { get; set; }
}

// ─────────────────────────────────────────────────────────────
// Trade tax
// ─────────────────────────────────────────────────────────────

/// <summary>
/// CII ApplicableTradeTax (ram) — per-rate tax breakdown at header level.
/// </summary>
public class CiiApplicableTradeTax
{
    /// <summary>Tax calculated amount (tax = basis × rate).</summary>
    [XmlElement("CalculatedAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? CalculatedAmount { get; set; }

    /// <summary>Tax basis amount (the amount the tax is applied to).</summary>
    [XmlElement("BasisAmount", Namespace = CiiNamespaces.Ram)]
    public CiiAmountType? BasisAmount { get; set; }

    /// <summary>Tax rate percentage.</summary>
    [XmlElement("ApplicablePercent", Namespace = CiiNamespaces.Ram)]
    public CiiPercentType? ApplicablePercent { get; set; }

    /// <summary>Tax category code (S, Z, E, AE, K, G, O, L, M per Peppol).</summary>
    [XmlElement("CategoryCode", Namespace = CiiNamespaces.Ram)]
    public string? CategoryCode { get; set; }

    /// <summary>Tax type code (e.g., "VAT").</summary>
    [XmlElement("TypeCode", Namespace = CiiNamespaces.Ram)]
    public string? TypeCode { get; set; }
}
