namespace InvoiceFlow.Infrastructure.Formats.Zugferd.Models;

/// <summary>
/// ZUGFeRD/Factur-X conformance profiles.
/// Each profile defines the minimum required CII elements for a specific use case.
/// See: https://www.ferd-net.de/standards/zugferd-2.1/zugferd-2.1-profiluebersicht/index.html
/// </summary>
public enum ZugferdProfile
{
    /// <summary>ZUGFeRD Minimum — basic summary data only, no line items.</summary>
    Minimum,

    /// <summary>ZUGFeRD BASIC Without Lines — header data without line-level detail.</summary>
    BasicWl,

    /// <summary>ZUGFeRD BASIC — line items included, EN 16931 subset.</summary>
    Basic,

    /// <summary>Full EN 16931 conformance — European norm compliant.</summary>
    En16931,

    /// <summary>ZUGFeRD Extended — additional data beyond EN 16931.</summary>
    Extended
}
