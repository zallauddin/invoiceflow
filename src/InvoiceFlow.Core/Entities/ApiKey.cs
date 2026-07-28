namespace InvoiceFlow.Core.Entities;

/// <summary>
/// API key entity — stored in the database and used for programmatic access
/// by third-party integrations and automated systems.
/// </summary>
public class ApiKey
{
    /// <summary>Unique API key identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this API key belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Display name for this API key (e.g., "Production Integration").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The actual API key value (hashed in production).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>JSON array of allowed permissions/scopes.</summary>
    public string? Permissions { get; set; }

    /// <summary>Whether this API key is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the key was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last use.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>UTC timestamp when the key expires (null = never).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
