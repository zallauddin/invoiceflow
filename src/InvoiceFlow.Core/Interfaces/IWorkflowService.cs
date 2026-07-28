using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Result of processing an approval step.
/// </summary>
public record ApprovalStepResult
{
    public bool Success { get; init; }
    public Guid? NextStepId { get; init; }
    public string? NextStepName { get; init; }
    public bool IsChainComplete { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Manages multi-step approval workflows. Routes invoices through
/// configurable approval chains with deadline enforcement and escalation.
/// </summary>
public interface IWorkflowService
{
    /// <summary>Starts the approval workflow for an invoice by initializing step tracking.</summary>
    Task<ApprovalStepResult> StartApprovalAsync(
        Guid invoiceId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Approves the current step and advances to the next step (if any).</summary>
    Task<ApprovalStepResult> ApproveStepAsync(
        Guid approvalRequestId,
        Guid userId,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>Rejects the current step, stopping the entire chain.</summary>
    Task<ApprovalStepResult> RejectStepAsync(
        Guid approvalRequestId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Checks for any steps that have exceeded their deadline and processes escalations.</summary>
    Task<int> ProcessEscalationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the active chain configuration for a given invoice.</summary>
    Task<ApprovalChain?> ResolveChainForInvoiceAsync(
        Guid tenantId,
        string countryCode,
        decimal totalAmount,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current status of an approval chain for an invoice.</summary>
    Task<ApprovalChainStatus> GetChainStatusAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Status of an approval chain for a specific invoice.
/// </summary>
public record ApprovalChainStatus
{
    public Guid InvoiceId { get; init; }
    public string? ChainName { get; init; }
    public int TotalSteps { get; init; }
    public int CompletedSteps { get; init; }
    public string CurrentStatus { get; init; } = "pending";
    public DateTime? Deadline { get; init; }
    public List<StepStatus> Steps { get; init; } = new();
}

public record StepStatus
{
    public int StepOrder { get; init; }
    public string StepName { get; init; } = string.Empty;
    public string Status { get; init; } = "pending"; // pending, approved, rejected, escalated, skipped
    public string? ApprovedBy { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? Deadline { get; init; }
}
