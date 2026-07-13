using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>Tax summary (DatiRiepilogo) — aggregated tax data per rate/exemption code.
/// One entry per unique AliquotaIVA + Natura combination.</summary>
public class DatiRiepilogoType
{
    /// <summary>VAT rate percentage (mandatory, 0.00 for exempt items).</summary>
    [XmlElement("AliquotaIVA")]
    public decimal AliquotaIVA { get; set; }

    /// <summary>VAT exemption code (mandatory when AliquotaIVA = 0): N1-N6.</summary>
    [XmlElement("Natura")]
    public string? Natura { get; set; }

    /// <summary>Taxable amount for this rate (mandatory).</summary>
    [XmlElement("ImponibileImporto")]
    public decimal ImponibileImporto { get; set; }

    /// <summary>Tax amount for this rate (mandatory, = ImponibileImporto * AliquotaIVA / 100).</summary>
    [XmlElement("Imposta")]
    public decimal Imposta { get; set; }

    /// <summary>VAT collectibility: I=mandatory, D=deductible, E=none (optional).</summary>
    [XmlElement("EsigibilitaIVA")]
    public string? EsigibilitaIVA { get; set; }

    /// <summary>Tax reference data (optional, for reverse charge and similar).</summary>
    [XmlElement("RiferimentoNormativo")]
    public string? RiferimentoNormativo { get; set; }
}
