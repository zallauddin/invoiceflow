namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

/// <summary>
/// UN/CEFACT CII (Cross-Industry Invoice) XML namespace constants.
/// These URIs are defined by the ISO 20628 / EN 16931 standards.
/// </summary>
public static class CiiNamespaces
{
    /// <summary>Root message (rsm) — CrossIndustryInvoice root element.</summary>
    public const string Rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";

    /// <summary>Qualified data types (qdt) — typed values like DateTimeString.</summary>
    public const string Qdt = "urn:un:unece:uncefact:data:standard:QualifiedDataType:100";

    /// <summary>Unqualified data types (udt) — untyped values.</summary>
    public const string Udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

    /// <summary>Reusable aggregate business information model (ram) — structural elements.</summary>
    public const string Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationModel:100";

    /// <summary>ZUGFeRD 2.1 EN 16931 compliant profile URI.</summary>
    public const string En16931ProfileId = "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2.1:basic";

    /// <summary>ZUGFeRD 2.1 EXTENDED profile URI.</summary>
    public const string ExtendedProfileId = "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2.1:extended";

    /// <summary>ZUGFeRD 2.1 MINIMUM profile URI.</summary>
    public const string MinimumProfileId = "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2.1:minimum";

    /// <summary>ZUGFeRD 2.1 BASIC WL profile URI.</summary>
    public const string BasicWlProfileId = "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2.1:basicwl";

    /// <summary>ZUGFeRD 2.1 BASIC profile URI (with line items).</summary>
    public const string BasicProfileId = "urn:cen.eu:en16931:2017#compliant#urn:zugferd.de:2.1:basic";

    /// <summary>Map from profile enum to URI string.</summary>
    public static string GetProfileUri(ZugferdProfile profile) => profile switch
    {
        ZugferdProfile.Minimum => MinimumProfileId,
        ZugferdProfile.BasicWl => BasicWlProfileId,
        ZugferdProfile.Basic => BasicProfileId,
        ZugferdProfile.En16931 => En16931ProfileId,
        ZugferdProfile.Extended => ExtendedProfileId,
        _ => En16931ProfileId
    };

    /// <summary>Map from URI string to profile enum (case-insensitive).</summary>
    public static ZugferdProfile? ParseProfileUri(string? profileUri)
    {
        if (string.IsNullOrWhiteSpace(profileUri))
            return null;

        if (profileUri.Contains("minimum", StringComparison.OrdinalIgnoreCase))
            return ZugferdProfile.Minimum;
        if (profileUri.Contains("basicwl", StringComparison.OrdinalIgnoreCase))
            return ZugferdProfile.BasicWl;
        if (profileUri.Contains("extended", StringComparison.OrdinalIgnoreCase))
            return ZugferdProfile.Extended;
        if (profileUri.Contains("basic", StringComparison.OrdinalIgnoreCase))
            return ZugferdProfile.Basic;
        if (profileUri.Contains("en16931", StringComparison.OrdinalIgnoreCase))
            return ZugferdProfile.En16931;

        return ZugferdProfile.En16931; // default fallback
    }
}
