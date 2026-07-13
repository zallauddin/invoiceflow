using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>Sender identification (IdTrasmittente) — IdPaese (2-char ISO) + IdCodice (1-28 chars).</summary>
public class IdTrasmittenteType
{
    /// <summary>Country code (ISO 3166-1 alpha-2, e.g., "IT").</summary>
    [XmlElement("IdPaese")]
    public string IdPaese { get; set; } = string.Empty;

    /// <summary>Tax code / fiscal code of the sender (1-28 chars).</summary>
    [XmlElement("IdCodice")]
    public string IdCodice { get; set; } = string.Empty;
}

/// <summary>VAT identification (IdFiscaleIVA) — IdPaese (2-char ISO) + IdCodice (1-28 chars).</summary>
public class IdFiscaleIVAType
{
    /// <summary>Country code (ISO 3166-1 alpha-2, e.g., "IT").</summary>
    [XmlElement("IdPaese")]
    public string IdPaese { get; set; } = string.Empty;

    /// <summary>VAT number / fiscal code (1-28 chars).</summary>
    [XmlElement("IdCodice")]
    public string IdCodice { get; set; } = string.Empty;
}

/// <summary>Party name details — Denominazione for legal entities, Nome+Cognome for individuals.</summary>
public class AnagraficaType
{
    /// <summary>Legal entity name (e.g., "Acme S.r.l."). Used for companies.</summary>
    [XmlElement("Denominazione")]
    public string? Denominazione { get; set; }

    /// <summary>First name (for individual persons).</summary>
    [XmlElement("Nome")]
    public string? Nome { get; set; }

    /// <summary>Surname (for individual persons).</summary>
    [XmlElement("Cognome")]
    public string? Cognome { get; set; }

    /// <summary>Titolo (honorific title, e.g., "Dr.", "Sig."). Optional.</summary>
    [XmlElement("Titolo")]
    public string? Titolo { get; set; }

    /// <summary>PEC email (optional).</summary>
    [XmlElement("Email")]
    public string? Email { get; set; }

    /// <summary>Telephone (optional).</summary>
    [XmlElement("Telefono")]
    public string? Telefono { get; set; }

    /// <summary>Fax (optional).</summary>
    [XmlElement("Fax")]
    public string? Fax { get; set; }

    /// <summary>Registered email (optional, for legal entities).</summary>
    [XmlElement("RegimeFiscale")]
    public string? RegimeFiscale { get; set; }
}

/// <summary>Registered office address (Sede) — mandatory for all parties.</summary>
public class SedeType
{
    /// <summary>Street address (mandatory).</summary>
    [XmlElement("Indirizzo")]
    public string Indirizzo { get; set; } = string.Empty;

    /// <summary>Building number (optional).</summary>
    [XmlElement("NumeroCivico")]
    public string? NumeroCivico { get; set; }

    /// <summary>Italian postal code — CAP (5 digits, mandatory).</summary>
    [XmlElement("CAP")]
    public string CAP { get; set; } = string.Empty;

    /// <summary>Municipality / city name (mandatory).</summary>
    [XmlElement("Comune")]
    public string Comune { get; set; } = string.Empty;

    /// <summary>Province code (2 chars, optional for non-Italian addresses).</summary>
    [XmlElement("Provincia")]
    public string? Provincia { get; set; }

    /// <summary>Country code (ISO 3166-1 alpha-2, mandatory, e.g., "IT").</summary>
    [XmlElement("Nazione")]
    public string Nazione { get; set; } = "IT";
}

/// <summary>Contact details (Contatti) — telephone and email.</summary>
public class ContattiType
{
    /// <summary>Telephone number (optional).</summary>
    [XmlElement("Telefono")]
    public string? Telefono { get; set; }

    /// <summary>Email address (optional).</summary>
    [XmlElement("Email")]
    public string? Email { get; set; }

    /// <summary>Fax number (optional).</summary>
    [XmlElement("Fax")]
    public string? Fax { get; set; }
}

/// <summary>REA (Repertorio Economico Amministrativo) registration data.</summary>
public class IscrizioneREAType
{
    /// <summary>Province office code (2 chars).</summary>
    [XmlElement("Ufficio")]
    public string Ufficio { get; set; } = string.Empty;

    /// <summary>REA registration number.</summary>
    [XmlElement("NumeroREA")]
    public string NumeroREA { get; set; } = string.Empty;

    /// <summary>Share capital (optional).</summary>
    [XmlElement("CapitaleSociale")]
    public decimal? CapitaleSociale { get; set; }

    /// <summary>Sole shareholder indicator: SU = sole shareholder, SP = none.</summary>
    [XmlElement("SocioUnico")]
    public string? SocioUnico { get; set; }

    /// <summary>Liquidation status: LN = in liquidation, LS = none.</summary>
    [XmlElement("StatoLiquidazione")]
    public string? StatoLiquidazione { get; set; }
}

/// <summary>Seller/supplier fiscal identification data (DatiAnagrafici CedentePrestatore).</summary>
public class DatiAnagraficiCedenteType
{
    /// <summary>VAT identification number (mandatory for VAT-registered sellers).</summary>
    [XmlElement("IdFiscaleIVA")]
    public IdFiscaleIVAType? IdFiscaleIVA { get; set; }

    /// <summary>Italian fiscal code (16 chars for individuals, 11 chars for entities).</summary>
    [XmlElement("CodiceFiscale")]
    public string? CodiceFiscale { get; set; }

    /// <summary>Party name details (mandatory).</summary>
    [XmlElement("Anagrafica")]
    public AnagraficaType Anagrafica { get; set; } = new();

    /// <summary>Tax regime code (mandatory): RF01=Ordinario, RF02=Minimi, RF04=Semplificato, etc.</summary>
    [XmlElement("RegimeFiscale")]
    public string RegimeFiscale { get; set; } = "RF01";
}

/// <summary>Buyer/customer fiscal identification data (DatiAnagrafici CessionarioCommittente).</summary>
public class DatiAnagraficiCessionarioType
{
    /// <summary>VAT identification number (optional for non-VAT-registered buyers).</summary>
    [XmlElement("IdFiscaleIVA")]
    public IdFiscaleIVAType? IdFiscaleIVA { get; set; }

    /// <summary>Italian fiscal code (16 chars for individuals, 11 chars for entities).</summary>
    [XmlElement("CodiceFiscale")]
    public string? CodiceFiscale { get; set; }

    /// <summary>Party name details (mandatory).</summary>
    [XmlElement("Anagrafica")]
    public AnagraficaType Anagrafica { get; set; } = new();
}

/// <summary>Fiscal representative identification data (DatiAnagrafici RappresentanteFiscale).</summary>
public class DatiAnagraficiRappresentanteType
{
    /// <summary>VAT identification number.</summary>
    [XmlElement("IdFiscaleIVA")]
    public IdFiscaleIVAType? IdFiscaleIVA { get; set; }

    /// <summary>Italian fiscal code.</summary>
    [XmlElement("CodiceFiscale")]
    public string? CodiceFiscale { get; set; }

    /// <summary>Representative name details.</summary>
    [XmlElement("Anagrafica")]
    public AnagraficaType Anagrafica { get; set; } = new();
}
