namespace InvoiceFlow.Infrastructure.Compliance.Zatca.Models;

/// <summary>
/// Configuration for the ZATCA e-invoicing API integration.
/// Bound from the "Zatca" section of IConfiguration.
/// </summary>
public sealed class ZatcaApiConfig
{
    /// <summary>Configuration section name used for binding.</summary>
    public const string SectionName = "Zatca";

    /// <summary>Base URL of the ZATCA API (e.g., https://api.zatca.gov.sa for production).</summary>
    public required string ApiBaseUrl { get; init; }

    /// <summary>Compliance Solution Identifier (CSID) for API authentication.</summary>
    public required string Csid { get; init; }

    /// <summary>Secret key associated with the CSID.</summary>
    public required string Secret { get; init; }

    /// <summary>Whether to use the ZATCA sandbox (simulation) environment.</summary>
    public bool SandboxMode { get; init; } = true;

    /// <summary>Device serial number registered with ZATCA for compliance clearance.</summary>
    public required string DeviceSerialNumber { get; init; }
}
