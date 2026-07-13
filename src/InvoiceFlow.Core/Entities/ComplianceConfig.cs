using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Per-tenant compliance configuration for a specific country and model.</summary>
public class ComplianceConfig
{
    /// <summary>Unique configuration identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this configuration belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>Compliance model to use for this country.</summary>
    public ComplianceModel Model { get; set; }

    /// <summary>Whether this configuration is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether to use sandbox/test endpoints.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>JSON-serialized provider-specific configuration (API keys, endpoints).</summary>
    public string? ConfigJson { get; set; }

    /// <summary>UTC timestamp when the configuration was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
