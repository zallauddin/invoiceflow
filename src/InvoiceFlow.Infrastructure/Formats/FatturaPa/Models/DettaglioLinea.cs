using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>Product code (CodiceArticolo) — optional product/item identifiers.</summary>
public class CodiceArticoloType
{
    /// <summary>Code value (mandatory when CodiceArticolo is present).</summary>
    [XmlElement("CodiceValore")]
    public string CodiceValore { get; set; } = string.Empty;

    /// <summary>Code type (e.g., "SKU", "EAN", "CPV", "022" for UNSPSC).</summary>
    [XmlElement("CodiceTipo")]
    public string? CodiceTipo { get; set; }
}

/// <summary>Line item detail (DettaglioLinea) — individual goods or services on the invoice.
/// Each line represents a single billable item with quantity, unit price, and tax info.</summary>
public class DettaglioLineaType
{
    /// <summary>Line number (mandatory, sequential, 1-based).</summary>
    [XmlElement("NumeroLinea")]
    public int NumeroLinea { get; set; }

    /// <summary>Line type code (optional, same values as TipoDocumento).</summary>
    [XmlElement("TipoCessionePrestazione")]
    public string? TipoCessionePrestazione { get; set; }

    /// <summary>Product codes (optional, zero or more).</summary>
    [XmlElement("CodiceArticolo")]
    public List<CodiceArticoloType>? CodiceArticolo { get; set; }

    /// <summary>Line description (mandatory, max 1000 chars).</summary>
    [XmlElement("Descrizione")]
    public string Descrizione { get; set; } = string.Empty;

    /// <summary>Quantity (optional for free items).</summary>
    [XmlElement("Quantita")]
    public decimal? Quantita { get; set; }

    /// <summary>Unit of measure code (e.g., "PCE", "H", "KGM"). Optional.</summary>
    [XmlElement("UnitaMisura")]
    public string? UnitaMisura { get; set; }

    /// <summary>Unit price (mandatory for priced lines).</summary>
    [XmlElement("PrezzoUnitario")]
    public decimal PrezzoUnitario { get; set; }

    /// <summary>Line total amount (mandatory, = Quantita * PrezzoUnitario).</summary>
    [XmlElement("PrezzoTotale")]
    public decimal PrezzoTotale { get; set; }

    /// <summary>VAT rate percentage (mandatory, 0.00 for exempt items).</summary>
    [XmlElement("AliquotaIVA")]
    public decimal AliquotaIVA { get; set; }

    /// <summary>VAT exemption code (mandatory when AliquotaIVA = 0): N1-N6.</summary>
    [XmlElement("Natura")]
    public string? Natura { get; set; }

    /// <summary>Spese accessorie (accessory charges, optional).</summary>
    [XmlElement("SpeseAccessorie")]
    public decimal? SpeseAccessorie { get; set; }

    /// <summary>Art.73 indicator (optional, "S" if using Art.73 procedure).</summary>
    [XmlElement("Art73")]
    public string? Art73 { get; set; }
}
