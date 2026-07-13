namespace InvoiceFlow.Infrastructure.Formats.Cii.Models;

/// <summary>UN/CEFACT Cross-Industry Invoice (CII) D10B XML namespace constants.</summary>
public static class CiiNamespaces
{
    /// <summary>Root / Standard namespace (rsm:CrossIndustryInvoice).</summary>
    public const string Rsm = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";

    /// <summary>Qualified DataType namespace (qdt).</summary>
    public const string Qdt = "urn:un:unece:uncefact:data:standard:QualifiedDataType:100";

    /// <summary>Unqualified DataType namespace (udt).</summary>
    public const string Udt = "urn:un:unece:uncefact:data:standard:UnqualifiedDataType:100";

    /// <summary>Reusable Aggregate Business Information Model namespace (ram).</summary>
    public const string Ram = "urn:un:unece:uncefact:data:standard:ReusableAggregateBusinessInformationModel:100";
}
