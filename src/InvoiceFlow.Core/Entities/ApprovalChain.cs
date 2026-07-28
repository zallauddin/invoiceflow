namespace InvoiceFlow.Core.Entities;

/// <summary>
/// Defines an approval workflow chain — a named sequence of approval steps
/// that an invoice (or other document) must traverse before final approval.
///
/// Example Chain: "Three-Way Matching"
///   Step 1: Manager Review  (role: User,  deadline: 24h, escalate → Step 2)
///   Step 2: Finance Review  (role: User,  deadline: 48h, escalate → Admin)
///   Step 3: Director Approve (role: Admin, deadline: 72h, escalate → auto-approve)
/// </summary>
public class ApprovalChain
{
    /// <summary>Unique chain identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this chain belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Display name for this workflow chain.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The entity type this chain applies to.
    /// Matches DocumentType enum: Invoice, CreditNote, PurchaseOrder, etc.
    /// </summary>
    public string TargetEntityType { get; set; } = "Invoice";

    /// <summary>Whether this chain is active and should be used for new documents.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional description of when this chain applies.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional country filter. If set, this chain only applies to documents with this country code.
    /// </summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// Optional minimum total amount threshold. If set, this chain only applies
    /// to documents with TotalAmount >= this value.
    /// </summary>
    public decimal? MinTotalAmount { get; set; }

    /// <summary>
    /// Optional maximum total amount threshold.
    /// </summary>
    public decimal? MaxTotalAmount { get; set; }

    /// <summary>UTC timestamp when the chain was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Steps in this chain, ordered by StepOrder.</summary>
    public List<ApprovalStep> Steps { get; set; } = new();

    /// <summary>Navigation property to the tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
