namespace InvoiceFlow.Infrastructure.Compliance.Peppol;

/// <summary>
/// Wraps an invoice for PEPPOL transmission with UBL XML content and metadata
/// required by the Access Point routing and document type identification.
/// </summary>
public sealed class PeppolDocumentWrapper
{
    /// <summary>The serialized UBL 2.1 XML content of the document.</summary>
    public string UblXmlContent { get; set; } = string.Empty;

    /// <summary>
    /// UBL document type namespace URI (e.g., "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2").
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// PEPPOL process type URI identifying the business process and profile
    /// (e.g., "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:www.cenbii.eu:transaction:biicoretrdm010:ver2.0#urn:www.peppol.eu:bis:peppol4a:ver2.0").
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;
}
