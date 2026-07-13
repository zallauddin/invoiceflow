namespace InvoiceFlow.Infrastructure.Compliance.Poland.Models;

/// <summary>
/// Configuration for the Polish KSeF (Krajowy System e-Faktur) compliance integration.
/// </summary>
public sealed class PolandKsefConfig
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "PolandKsef";

    /// <summary>Base URL of the KSeF API endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>NIP (Numer Identyfikacji Podatkowej) — Polish tax identification number.</summary>
    public string Nip { get; set; } = string.Empty;

    /// <summary>When <c>true</c>, uses the KSeF sandbox/test environment.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>Authentication token for KSeF API access.</summary>
    public string? Token { get; set; }

    /// <summary>KSeF environment type: "PROD" for production, "TEST" for testing.</summary>
    public string EnvironmentType { get; set; } = "TEST";
}
