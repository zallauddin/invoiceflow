using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Response DTO for document-to-document relationships.</summary>
public sealed record DocumentRelationshipDto
{
    /// <summary>Unique relationship identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Source document in the relationship.</summary>
    public required Guid SourceDocumentId { get; init; }

    /// <summary>Target document in the relationship.</summary>
    public required Guid TargetDocumentId { get; init; }

    /// <summary>Tenant this relationship belongs to (denormalized for query performance).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Tenant of the target document (denormalized for cross-tenant validation).</summary>
    public Guid? TargetTenantId { get; init; }

    /// <summary>Type of relationship between the two documents.</summary>
    public required DocumentRelationshipType RelationshipType { get; init; }

    /// <summary>Optional description of the relationship.</summary>
    public string? Description { get; init; }

    /// <summary>JSON object with additional metadata about the relationship.</summary>
    public string? Metadata { get; init; }

    /// <summary>User who created the relationship, or "SYSTEM" for automated relationships.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Optional FK to the user who created this relationship.</summary>
    public Guid? CreatedByUserId { get; init; }

    /// <summary>UTC timestamp when the relationship was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp when the relationship was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }
}
