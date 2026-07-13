namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Supported electronic invoice formats.</summary>
public enum InvoiceFormat
{
    /// <summary>UBL 2.1 Invoice/CreditNote (OASIS standard).</summary>
    Ubl21,

    /// <summary>German CIUS of UBL 2.1 (XRechnung).</summary>
    XRechnung,

    /// <summary>ZUGFeRD/Factur-X (PDF-A3 with embedded XML).</summary>
    Zugferd,

    /// <summary>Italian FatturaPA (p:Fattura XML).</summary>
    FatturaPA,

    /// <summary>Austrian ebInterface.</summary>
    EbInterface,

    /// <summary>UN/CEFACT Cross-Industry Invoice (CII).</summary>
    Cii,

    /// <summary>EDIFACT D96A.</summary>
    Edifact,

    /// <summary>SAP IDoc.</summary>
    Idoc,

    /// <summary>Mexico CFDI 4.0.</summary>
    Cfdi,

    /// <summary>Brazil NF-e.</summary>
    Nfe,

    /// <summary>India e-Invoice IRP.</summary>
    Irp,

    /// <summary>Plain PDF (no embedded XML).</summary>
    Pdf,

    /// <summary>CSV/tabular format.</summary>
    Csv,

    /// <summary>Format could not be determined.</summary>
    Unknown
}
