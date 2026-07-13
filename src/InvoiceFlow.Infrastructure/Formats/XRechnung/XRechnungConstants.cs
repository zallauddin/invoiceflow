namespace InvoiceFlow.Infrastructure.Formats.XRechnung;

/// <summary>
/// Constants for XRechnung 3.0 — the German CIUS (Core Invoice Usage Specification)
/// built on top of UBL 2.1 / EN 16931.
/// </summary>
public static class XRechnungConstants
{
    /// <summary>BT-22: XRechnung CustomizationID per KOSIT standard.</summary>
    public const string CustomizationId =
        "urn:cen.eu:en16931:2017#compliant#urn:xoev-de:kosit:standard:xrechnung_3.0";

    /// <summary>BT-23: Peppol Billing 01:1.0 profile identifier (same as EN 16931 Peppol BIS).</summary>
    public const string ProfileId = "urn:fdc:peppol.eu:2017:poacc:billing:01:1.0";

    /// <summary>BT-24: UBL version, always 2.1 for XRechnung.</summary>
    public const string UblVersion = "2.1";

    /// <summary>XRechnung specification version label.</summary>
    public const string Version = "3.0";

    /// <summary>German tax category codes per EN 16931 / XRechnung.</summary>
    public static readonly string[] ValidGermanTaxCategories = ["S", "Z", "E", "AE", "K", "G", "O", "L"];

    /// <summary>Valid XRechnung InvoiceTypeCode values (BT-3).</summary>
    public static readonly string[] ValidInvoiceTypeCodes = ["380", "381", "384", "389"];
}
