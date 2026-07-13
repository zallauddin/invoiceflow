namespace InvoiceFlow.Infrastructure.Formats.Ubl21.Models;

/// <summary>UBL 2.1 XML namespace constants per OASIS specification.</summary>
public static class UblNamespaces
{
    /// <summary>Invoice document namespace.</summary>
    public const string Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    /// <summary>CreditNote document namespace.</summary>
    public const string CreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    /// <summary>Common Basic Components namespace (cbc).</summary>
    public const string Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>Common Aggregate Components namespace (cac).</summary>
    public const string Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    /// <summary>Standard document instance namespace (UBL 2.1).</summary>
    public const string Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    /// <summary>Schema location for UBL 2.1 Invoice.</summary>
    public const string InvoiceSchemaLocation =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2 " +
        "http://docs.oasis-open.org/ubl/os-UBL-2.1/xsd/maindoc/UBL-Invoice-2.1.xsd";

    /// <summary>Schema location for UBL 2.1 CreditNote.</summary>
    public const string CreditNoteSchemaLocation =
        "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2 " +
        "http://docs.oasis-open.org/ubl/os-UBL-2.1/xsd/maindoc/UBL-CreditNote-2.1.xsd";
}
