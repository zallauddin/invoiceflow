namespace InvoiceFlow.Infrastructure.Formats.FatturaPa.Models;

/// <summary>FatturaPA v1.2 XML namespace constants per Agenzia delle Entrate specification.</summary>
public static class FatturaPaNamespaces
{
    /// <summary>Root namespace for FatturaPA v1.2.</summary>
    public const string Root = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    /// <summary>Standard prefix for the FatturaPA root namespace.</summary>
    public const string Prefix = "p";

    /// <summary>XML digital signature namespace (ds:).</summary>
    public const string Dsig = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>XML Schema instance namespace (xsi:).</summary>
    public const string Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>Schema location for FatturaPA v1.2.</summary>
    public const string SchemaLocation =
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2 " +
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2/FatturaPA_v1.2.xsd";
}
