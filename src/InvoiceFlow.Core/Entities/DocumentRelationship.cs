using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Relationship between two documents.</summary>
public class DocumentRelationship
{
    /// <summary>Unique relationship identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Source document in the relationship.</summary>
    public Guid SourceDocumentId { get; set; }

    /// <summary>Target document in the relationship.</summary>
    public Guid TargetDocumentId { get; set; }

    /// <summary>Tenant this relationship belongs to (denormalized for query performance).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Type of relationship.</summary>
    public DocumentRelationshipType RelationshipType { get; set; }

    /// <summary>Optional description of the relationship.</summary>
    public string? Description { get; set; }

    /// <summary>JSON object with additional metadata about the relationship.</summary>
    public string? Metadata { get; set; }

    /// <summary>User who created the relationship, or "SYSTEM" for automated relationships.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Optional FK to the user who created this relationship.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>UTC timestamp when the relationship was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the relationship was last updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Tenant of the target document (denormalized for cross-tenant validation).</summary>
    public Guid? TargetTenantId { get; set; }

    /// <summary>Navigation property to the source document.</summary>
    public Document SourceDocument { get; set; } = null!;

    /// <summary>Navigation property to the target document.</summary>
    public Document TargetDocument { get; set; } = null!;

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Navigation property to the user who created this relationship.</summary>
    public User? CreatedByUser { get; set; }

    /// <summary>Navigation property to the tenant of the target document.</summary>
    public Tenant? TargetTenant { get; set; }
}