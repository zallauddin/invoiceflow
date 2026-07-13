using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Represents an approval request for an invoice.</summary>
public class ApprovalRequest
{
    /// <summary>Unique approval request identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Invoice requiring approval.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Tenant this approval belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Current approval status.</summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>Comments from the approver or requester.</summary>
    public string? Comments { get; set; }

    /// <summary>User this approval is assigned to.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>User who reviewed this request.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>UTC timestamp when the approval was requested.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the approval was reviewed.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Navigation property to the invoice.</summary>
    public Invoice Invoice { get; set; } = null!;

    /// <summary>Navigation property to the assigned user.</summary>
    public User? AssignedToUser { get; set; }

    /// <summary>Navigation property to the reviewing user.</summary>
    public User? ReviewedByUser { get; set; }
}
