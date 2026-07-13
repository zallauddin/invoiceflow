using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>Transmission data (DatiTrasmissione) — sender/recipient routing information.</summary>
public class DatiTrasmissioneType
{
    /// <summary>Sender identification (IdPaese + IdCodice).</summary>
    [XmlElement("IdTrasmittente")]
    public IdTrasmittenteType IdTrasmittente { get; set; } = new();

    /// <summary>Progressive message number (max 5 chars).</summary>
    [XmlElement("ProgressivoInvio")]
    public string ProgressivoInvio { get; set; } = string.Empty;

    /// <summary>Transmission format: FPA12 (domestic) or FPR12 (cross-border/PA).</summary>
    [XmlElement("FormatoTrasmissione")]
    public string FormatoTrasmissione { get; set; } = "FPA12";

    /// <summary>Recipient code (6 chars for PA, 7 chars for private, 0000000 + PEC for others).</summary>
    [XmlElement("CodiceDestinatario")]
    public string CodiceDestinatario { get; set; } = string.Empty;

    /// <summary>PEC email address (required when CodiceDestinatario is all zeros).</summary>
    [XmlElement("PECDestinatario")]
    public string? PECDestinatario { get; set; }

    /// <summary>Sender contact details.</summary>
    [XmlElement("ContattiTrasmittente")]
    public ContattiType? ContattiTrasmittente { get; set; }
}

/// <summary>Seller/supplier data (CedentePrestatore) — the invoice issuer.</summary>
public class CedentePrestatoreType
{
    /// <summary>Seller's fiscal identification data.</summary>
    [XmlElement("DatiAnagrafici")]
    public DatiAnagraficiCedenteType DatiAnagrafici { get; set; } = new();

    /// <summary>Seller's registered office address.</summary>
    [XmlElement("Sede")]
    public SedeType Sede { get; set; } = new();

    /// <summary>Stable organization address (optional, for non-resident sellers).</summary>
    [XmlElement("StabileOrganizzazione")]
    public SedeType? StabileOrganizzazione { get; set; }

    /// <summary>REA registration data (optional).</summary>
    [XmlElement("IscrizioneREA")]
    public IscrizioneREAType? IscrizioneREA { get; set; }

    /// <summary>Contact details (optional).</summary>
    [XmlElement("Contatti")]
    public ContattiType? Contatti { get; set; }

    /// <summary>Representative's fiscal data (optional).</summary>
    [XmlElement("RappresentanteFiscale")]
    public DatiAnagraficiRappresentanteType? RappresentanteFiscale { get; set; }
}

/// <summary>Buyer/customer data (CessionarioCommittente) — the invoice recipient.</summary>
public class CessionarioCommittenteType
{
    /// <summary>Buyer's fiscal identification data.</summary>
    [XmlElement("DatiAnagrafici")]
    public DatiAnagraficiCessionarioType DatiAnagrafici { get; set; } = new();

    /// <summary>Buyer's registered office address.</summary>
    [XmlElement("Sede")]
    public SedeType Sede { get; set; } = new();

    /// <summary>Stable organization address (optional).</summary>
    [XmlElement("StabileOrganizzazione")]
    public SedeType? StabileOrganizzazione { get; set; }

    /// <summary>Representative's fiscal data (optional).</summary>
    [XmlElement("RappresentanteFiscale")]
    public DatiAnagraficiRappresentanteType? RappresentanteFiscale { get; set; }

    /// <summary>Contact details (optional).</summary>
    [XmlElement("Contatti")]
    public ContattiType? Contatti { get; set; }
}

/// <summary>Third party intermediary / emitter subject (optional).</summary>
public class TerzoIntermediarioSoggettoEmittenteType
{
    /// <summary>Intermediary's fiscal identification data.</summary>
    [XmlElement("DatiAnagrafici")]
    public DatiAnagraficiRappresentanteType DatiAnagrafici { get; set; } = new();
}

/// <summary>FatturaElettronicaHeader — transmission, seller, and buyer information.</summary>
public class FatturaElettronicaHeaderType
{
    /// <summary>Transmission data (mandatory).</summary>
    [XmlElement("DatiTrasmissione")]
    public DatiTrasmissioneType DatiTrasmissione { get; set; } = new();

    /// <summary>Seller/supplier (mandatory).</summary>
    [XmlElement("CedentePrestatore")]
    public CedentePrestatoreType CedentePrestatore { get; set; } = new();

    /// <summary>Representative (optional, for fiscal representative).</summary>
    [XmlElement("RappresentanteFiscale")]
    public DatiAnagraficiRappresentanteType? RappresentanteFiscale { get; set; }

    /// <summary>Buyer/customer (mandatory).</summary>
    [XmlElement("CessionarioCommittente")]
    public CessionarioCommittenteType CessionarioCommittente { get; set; } = new();

    /// <summary>Third party intermediary (optional).</summary>
    [XmlElement("TerzoIntermediarioOSoggettoEmittente")]
    public TerzoIntermediarioSoggettoEmittenteType? TerzoIntermediario { get; set; }

    /// <summary>Subject who emits the document (optional, S = seller, T = third party).</summary>
    [XmlElement("SoggettoEmittente")]
    public string? SoggettoEmittente { get; set; }
}
