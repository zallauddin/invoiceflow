using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Response DTO for document version history entries.</summary>
public sealed record DocumentVersionHistoryDto
{
    /// <summary>Unique version history entry identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Document this history entry belongs to.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Tenant this document belongs to (denormalized for query performance).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Version number at the time of this change.</summary>
    public required int Version { get; init; }

    /// <summary>Type of change made.</summary>
    public required DocumentVersionChangeType ChangeType { get; init; }

    /// <summary>Description of the change.</summary>
    public string? Description { get; init; }

    /// <summary>JSON object with details of what changed (old/new values).</summary>
    public string? ChangeDetails { get; init; }

    /// <summary>JSON-serialized previous value of the changed field/property.</summary>
    public string? OldValue { get; init; }

    /// <summary>JSON-serialized new value of the changed field/property.</summary>
    public string? NewValue { get; init; }

    /// <summary>Name of the field/property that was changed (e.g., "Status", "StoragePath").</summary>
    public string? FieldName { get; init; }

    /// <summary>User who made the change, or "SYSTEM" for automated changes.</summary>
    public string? ChangedBy { get; init; }

    /// <summary>FK to the user who made the change.</summary>
    public Guid? ChangedByUserId { get; init; }

    /// <summary>UTC timestamp when the change was made.</summary>
    public required DateTime CreatedAt { get; init; }
}
