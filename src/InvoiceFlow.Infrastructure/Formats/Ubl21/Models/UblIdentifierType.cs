using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL identifier type with optional scheme URI.</summary>
public class UblIdentifierType
{
    /// <summary>The identifier value.</summary>
    [XmlText]
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional scheme identification URI (e.g., schemeID for tax IDs).</summary>
    [XmlAttribute("schemeID")]
    public string? SchemeId { get; set; }

    /// <summary>Optional scheme version URI.</summary>
    [XmlAttribute("schemeVersionID")]
    public string? SchemeVersionId { get; set; }
}
