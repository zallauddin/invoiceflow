using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

/// <summary>ebInterface document type enumeration — determines the kind of business document.</summary>
public enum EbDocumentType
{
    /// <summary>Standard invoice (Rechnung).</summary>
    [XmlEnum("Invoice")]
    Invoice,

    /// <summary>Credit note (Gutschrift).</summary>
    [XmlEnum("CreditNote")]
    CreditNote,

    /// <summary>Final settlement (Schlussrechnung).</summary>
    [XmlEnum("FinalSettlement")]
    FinalSettlement,

    /// <summary>Correction invoice (Korrekturrechnung).</summary>
    [XmlEnum("Correction")]
    Correction
}
