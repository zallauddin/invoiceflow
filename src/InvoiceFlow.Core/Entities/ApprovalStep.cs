namespace InvoiceFlow.Core.Entities;

/// <summary>
/// Defines a single step in an approval workflow chain.
/// Each step specifies which role can approve, the order in the chain,
/// a deadline for completion, and what happens when the deadline passes.
/// </summary>
public class ApprovalStep
{
    /// <summary>Unique step identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Workflow chain this step belongs to.</summary>
    public Guid ApprovalChainId { get; set; }

    /// <summary>Tenant this step belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Display name for this step (e.g., "Manager Review", "Finance Approval").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Order of this step in the chain (1-based). Lower numbers execute first.</summary>
    public int StepOrder { get; set; }

    /// <summary>
    /// The user role required to approve this step.
    /// Must match one of the UserRole enum values (Admin, User, Viewer).
    /// </summary>
    public string RequiredRole { get; set; } = "User";

    /// <summary>
    /// Optional specific user who must approve this step.
    /// If null, any user with the RequiredRole can approve.
    /// </summary>
    public Guid? AssignedUserId { get; set; }

    /// <summary>Maximum hours allowed for this step before escalation triggers.</summary>
    public int DeadlineHours { get; set; } = 48;

    /// <summary>
    /// Action to take when the deadline is exceeded.
    /// 0 = Escalate to next step, 1 = Auto-approve, 2 = Auto-reject, 3 = Notify admin only.
    /// </summary>
    public int EscalationAction { get; set; } = 0;

    /// <summary>Whether this step can be skipped if conditions are met.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Whether this step is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Navigation property to the workflow chain.</summary>
    public ApprovalChain ApprovalChain { get; set; } = null!;

    /// <summary>Navigation property to the assigned user.</summary>
    public User? AssignedUser { get; set; }

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
