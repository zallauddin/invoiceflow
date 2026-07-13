namespace InvoiceFlow.Infrastructure.Formats.EbInterface.Models;

/// <summary>ebInterface XML namespace constants per Austrian e-invoicing specification.</summary>
public static class EbInterfaceNamespaces
{
    /// <summary>ebInterface 6.0 namespace (current version).</summary>
    public const string V6 = "http://www.ebinterface.at/schema/6p0/";

    /// <summary>ebInterface 5.0 namespace (still in wide use).</summary>
    public const string V5 = "http://www.ebinterface.at/schema/5p0/";

    /// <summary>ebInterface 4.3 namespace (legacy).</summary>
    public const string V4 = "http://www.ebinterface.at/schema/4p3/";

    /// <summary>Default XML namespace prefix for ebInterface.</summary>
    public const string Prefix = "eb";
}
