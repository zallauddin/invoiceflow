using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL measure type with unit code attribute.</summary>
public class UblMeasureType
{
    /// <summary>The numeric value.</summary>
    [XmlText]
    public decimal Value { get; set; }

    /// <summary>UN/ECE Rec. 20 unit code (e.g., EA, H, KGM).</summary>
    [XmlAttribute("unitCode")]
    public string? UnitCode { get; set; }
}
