using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>ERP connector configuration for a tenant.</summary>
public class ConnectorConfig
{
    /// <summary>Unique connector configuration identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this connector belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Type of ERP system (SAP, Oracle, Xero, etc.).</summary>
    public ConnectorType ConnectorType { get; set; }

    /// <summary>Current status of the connector.</summary>
    public ConnectorStatus Status { get; set; } = ConnectorStatus.PendingAuth;

    /// <summary>JSON-serialized credentials (OAuth tokens, API keys).</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Whether to use sandbox/test mode.</summary>
    public bool SandboxMode { get; set; } = true;

    /// <summary>Direction of data synchronization.</summary>
    public SyncDirection SyncDirection { get; set; } = SyncDirection.Push;

    /// <summary>JSON-serialized additional configuration specific to the connector type.</summary>
    public string? ExtraConfigJson { get; set; }

    /// <summary>Sync interval in minutes, null for manual sync only.</summary>
    public int? SyncIntervalMinutes { get; set; }

    /// <summary>UTC timestamp of the last successful sync.</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>Total records successfully synced.</summary>
    public int? TotalSynced { get; set; }

    /// <summary>Number of failed sync attempts.</summary>
    public int? FailedSyncs { get; set; }

    /// <summary>UTC timestamp when the connector was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
