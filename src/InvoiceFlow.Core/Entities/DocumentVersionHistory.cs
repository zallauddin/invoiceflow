using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>History entry for document version changes.</summary>
public class DocumentVersionHistory
{
    /// <summary>Unique version history entry identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Document this history entry belongs to.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Tenant this document belongs to (denormalized for query performance).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Version number at the time of this change.</summary>
    public int Version { get; set; }

    /// <summary>Type of change made.</summary>
    public DocumentVersionChangeType ChangeType { get; set; }

    /// <summary>Description of the change.</summary>
    public string? Description { get; set; }

    /// <summary>JSON object with details of what changed (old/new values).</summary>
    public string? ChangeDetails { get; set; }

    /// <summary>JSON-serialized previous value of the changed field/property.</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON-serialized new value of the changed field/property.</summary>
    public string? NewValue { get; set; }

    /// <summary>Name of the field/property that was changed (e.g. "Status", "StoragePath"). Max 100 chars.</summary>
    public string? FieldName { get; set; }

    /// <summary>FK to User who made the change (nullable, optional).</summary>
    public Guid? ChangedByUserId { get; set; }

    /// <summary>Navigation to User who made the change.</summary>
    public User? ChangedByUser { get; set; }

    /// <summary>User who made the change, or "SYSTEM" for automated changes.</summary>
    public string? ChangedBy { get; set; }

    /// <summary>UTC timestamp when the change was made.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the document.</summary>
    public Document Document { get; set; } = null!;

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}