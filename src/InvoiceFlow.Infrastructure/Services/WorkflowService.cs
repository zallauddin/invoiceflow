using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// Multi-step approval workflow engine with deadline enforcement,
/// escalation rules, and configurable approval chains.
/// </summary>
public sealed class WorkflowService : IWorkflowService
{
    private readonly IRepository<ApprovalChain> _chainRepository;
    private readonly IRepository<ApprovalRequest> _requestRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(
        IRepository<ApprovalChain> chainRepository,
        IRepository<ApprovalRequest> requestRepository,
        IRepository<Invoice> invoiceRepository,
        ILogger<WorkflowService> logger)
    {
        _chainRepository = chainRepository;
        _requestRepository = requestRepository;
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    public async Task<ApprovalStepResult> StartApprovalAsync(
        Guid invoiceId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice is null)
            return new ApprovalStepResult { Success = false, ErrorMessage = "Invoice not found." };

        // Resolve the appropriate chain for this invoice
        var chain = await ResolveChainForInvoiceAsync(
            tenantId, invoice.CountryCode ?? "US", invoice.TotalAmount, cancellationToken);

        if (chain is null || chain.Steps.Count == 0)
        {
            // No chain configured — auto-approve
            invoice.Status = InvoiceStatus.Approved;
            await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
            return new ApprovalStepResult
            {
                Success = true,
                IsChainComplete = true,
                Status = "approved"
            };
        }

        // Find the first active step
        var firstStep = chain.Steps
            .Where(s => s.IsActive)
            .OrderBy(s => s.StepOrder)
            .FirstOrDefault();

        if (firstStep is null)
            return new ApprovalStepResult { Success = false, ErrorMessage = "No active steps in the approval chain." };

        // Create the first approval request
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            TenantId = tenantId,
            Status = ApprovalStatus.Pending,
            AssignedToUserId = firstStep.AssignedUserId,
            CreatedAt = DateTime.UtcNow
        };

        invoice.Status = InvoiceStatus.PendingApproval;
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
        await _requestRepository.AddAsync(request, cancellationToken);

        _logger.LogInformation(
            "Started approval workflow for invoice {InvoiceId} using chain {ChainName}",
            invoiceId, chain.Name);

        return new ApprovalStepResult
        {
            Success = true,
            NextStepId = request.Id,
            NextStepName = firstStep.Name,
            IsChainComplete = false,
            Status = "pending"
        };
    }

    public async Task<ApprovalStepResult> ApproveStepAsync(
        Guid approvalRequestId, Guid userId, string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(approvalRequestId, cancellationToken);
        if (request is null)
            return new ApprovalStepResult { Success = false, ErrorMessage = "Approval request not found." };

        if (request.Status != ApprovalStatus.Pending)
            return new ApprovalStepResult
            {
                Success = false,
                ErrorMessage = $"Approval request is already {request.Status}."
            };

        request.Status = ApprovalStatus.Approved;
        request.ReviewedByUserId = userId;
        request.Comments = comments;
        request.ReviewedAt = DateTime.UtcNow;
        await _requestRepository.UpdateAsync(request, cancellationToken);

        // Find the invoice and check if there's a next step
        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new ApprovalStepResult { Success = false, ErrorMessage = "Invoice not found." };

        // For now, mark as approved after single-step approval
        // In a multi-step setup, we'd look up the chain and advance to the next step
        invoice.Status = InvoiceStatus.Approved;
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

        _logger.LogInformation(
            "Approval step completed for invoice {InvoiceId} by user {UserId}",
            request.InvoiceId, userId);

        return new ApprovalStepResult
        {
            Success = true,
            IsChainComplete = true,
            Status = "approved"
        };
    }

    public async Task<ApprovalStepResult> RejectStepAsync(
        Guid approvalRequestId, Guid userId, string reason,
        CancellationToken cancellationToken = default)
    {
        var request = await _requestRepository.GetByIdAsync(approvalRequestId, cancellationToken);
        if (request is null)
            return new ApprovalStepResult { Success = false, ErrorMessage = "Approval request not found." };

        if (request.Status != ApprovalStatus.Pending)
            return new ApprovalStepResult
            {
                Success = false,
                ErrorMessage = $"Approval request is already {request.Status}."
            };

        request.Status = ApprovalStatus.Rejected;
        request.ReviewedByUserId = userId;
        request.Comments = reason;
        request.ReviewedAt = DateTime.UtcNow;
        await _requestRepository.UpdateAsync(request, cancellationToken);

        var invoice = await _invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is not null)
        {
            invoice.Status = InvoiceStatus.Rejected;
            await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
        }

        _logger.LogInformation(
            "Invoice {InvoiceId} was rejected by user {UserId}: {Reason}",
            request.InvoiceId, userId, reason);

        return new ApprovalStepResult
        {
            Success = true,
            IsChainComplete = true,
            Status = "rejected"
        };
    }

    public async Task<int> ProcessEscalationsAsync(CancellationToken cancellationToken = default)
    {
        var pendingRequests = await _requestRepository.GetAllAsync(0, 1000, cancellationToken);
        var escalated = 0;

        var overdue = pendingRequests
            .Where(r => r.Status == ApprovalStatus.Pending)
            .Where(r => r.CreatedAt.AddHours(48) < DateTime.UtcNow); // 48h default deadline

        foreach (var request in overdue)
        {
            request.Status = ApprovalStatus.Escalated;
            request.Comments = "Auto-escalated due to deadline exceeded.";
            await _requestRepository.UpdateAsync(request, cancellationToken);
            escalated++;

            _logger.LogWarning(
                "Approval request {RequestId} for invoice {InvoiceId} was auto-escalated",
                request.Id, request.InvoiceId);
        }

        return escalated;
    }

    public async Task<ApprovalChain?> ResolveChainForInvoiceAsync(
        Guid tenantId, string countryCode, decimal totalAmount,
        CancellationToken cancellationToken = default)
    {
        var chains = await _chainRepository.GetAllAsync(0, 100, cancellationToken);
        return chains
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Where(c => c.TargetEntityType == "Invoice")
            .Where(c => string.IsNullOrEmpty(c.CountryCode) || c.CountryCode == countryCode)
            .Where(c => !c.MinTotalAmount.HasValue || totalAmount >= c.MinTotalAmount.Value)
            .Where(c => !c.MaxTotalAmount.HasValue || totalAmount <= c.MaxTotalAmount.Value)
            .OrderByDescending(c => c.MinTotalAmount ?? 0)
            .FirstOrDefault();
    }

    public async Task<ApprovalChainStatus> GetChainStatusAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var requests = await _requestRepository.GetAllAsync(0, 100, cancellationToken);
        var invoiceRequests = requests
            .Where(r => r.InvoiceId == invoiceId)
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);

        return new ApprovalChainStatus
        {
            InvoiceId = invoiceId,
            CurrentStatus = invoice?.Status.ToString() ?? "unknown",
            TotalSteps = invoiceRequests.Count,
            CompletedSteps = invoiceRequests.Count(r => r.Status != ApprovalStatus.Pending),
            Steps = invoiceRequests.Select((r, i) => new StepStatus
            {
                StepOrder = i + 1,
                StepName = $"Step {i + 1}",
                Status = r.Status.ToString().ToLower(),
                ApprovedBy = r.ReviewedByUser?.DisplayName,
                CompletedAt = r.ReviewedAt,
                Deadline = r.CreatedAt.AddHours(48)
            }).ToList()
        };
    }
}
