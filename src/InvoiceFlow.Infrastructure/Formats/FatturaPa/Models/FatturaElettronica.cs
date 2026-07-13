using System.Xml.Serialization;

namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>FatturaPA root document — maps to the &lt;p:FatturaElettronica&gt; XML root element.
/// Italian mandatory e-invoicing format (v1.2) per Agenzia delle Entrate specification.</summary>
[XmlRoot("FatturaElettronica", Namespace = FatturaPaNamespaces.Root)]
public class FatturaElettronica
{
    /// <summary>Transmission format version (FPA12 = domestic, FPR12 = cross-border).</summary>
    [XmlAttribute("versione")]
    public string Versione { get; set; } = "FPA12";

    /// <summary>Optional emitter system identifier.</summary>
    [XmlElement("SistemaEmittente")]
    public string? SistemaEmittente { get; set; }

    /// <summary>Transmission and recipient data.</summary>
    [XmlElement("FatturaElettronicaHeader")]
    public FatturaElettronicaHeaderType Header { get; set; } = new();

    /// <summary>One or more invoice bodies (typically one per document).</summary>
    [XmlElement("FatturaElettronicaBody")]
    public List<FatturaElettronicaBodyType> Bodies { get; set; } = new();
}
