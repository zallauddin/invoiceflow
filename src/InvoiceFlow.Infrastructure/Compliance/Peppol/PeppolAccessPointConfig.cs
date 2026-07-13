namespace InvoiceFlow.Infrastructure.Compliance.Peppol;

/// <summary>
/// Configuration for connecting to a PEPPOL Access Point.
/// Maps from the "Peppol" section of IConfiguration.
/// </summary>
public sealed class PeppolAccessPointConfig
{
    /// <summary>Configuration section name used with IConfiguration binding.</summary>
    public const string SectionName = "Peppol";

    /// <summary>URL of the PEPPOL Access Point AS4 endpoint.</summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>Sender participant identifier (e.g., GLN or organization ID).</summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>Receiver participant identifier.</summary>
    public string ReceiverId { get; set; } = string.Empty;

    /// <summary>Username for basic authentication with the Access Point.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password for basic authentication with the Access Point.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Whether to use sandbox/test mode (skips actual HTTP transmission).</summary>
    public bool SandboxMode { get; set; } = true;
}
