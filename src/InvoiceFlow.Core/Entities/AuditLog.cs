namespace InvoiceFlow.Core.Entities;

/// <summary>Immutable audit log entry for GoBD/ViDA compliance.</summary>
/// <remarks>Entries are append-only. Each entry includes a hash chain for tamper detection.</remarks>
public class AuditLog
{
    /// <summary>Unique audit log entry identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this audit entry belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Related invoice, if applicable.</summary>
    public Guid? InvoiceId { get; set; }

    /// <summary>Action performed (e.g., EXTRACTION_COMPLETED, STATUS_CHANGED).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>User ID or "SYSTEM" that performed the action.</summary>
    public string? PerformedBy { get; set; }

    /// <summary>JSON payload with additional details about the action.</summary>
    public string? Details { get; set; }

    /// <summary>SHA-256 hash of the previous audit entry (for hash chain integrity).</summary>
    public string? PreviousHash { get; set; }

    /// <summary>SHA-256 hash of this audit entry content.</summary>
    public string? CurrentHash { get; set; }

    /// <summary>UTC timestamp when the action was performed.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the invoice.</summary>
    public Invoice? Invoice { get; set; }
}
