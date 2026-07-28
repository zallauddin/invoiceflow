using System.Security.Claims;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Approval workflow endpoints: chain management, step execution, and status tracking.
/// Supports multi-step approval with escalation and deadline enforcement.
/// </summary>
public static class ApprovalEndpoints
{
    private sealed record CreateChainInput(
        string Name,
        string TargetEntityType,
        string? Description,
        string? CountryCode,
        decimal? MinTotalAmount,
        decimal? MaxTotalAmount);

    private sealed record CreateStepInput(
        string Name,
        int StepOrder,
        string RequiredRole,
        Guid? AssignedUserId,
        int DeadlineHours = 48,
        int EscalationAction = 0,
        bool IsOptional = false);

    private sealed record ApproveInput(string? Comments);
    private sealed record RejectInput(string Reason);

    public static WebApplication MapApprovalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/approval")
            .WithTags("Approval Workflow")
            .RequireAuthorization("RequireApprover");

        // ─── Chain Management ────────────────────────────────

        // GET /api/approval/chains — List all approval chains
        group.MapGet("/chains", async (
            IRepository<ApprovalChain> repository,
            CancellationToken ct) =>
        {
            var chains = await repository.GetAllAsync(0, 100, ct);
            return Results.Ok(chains);
        })
        .WithName("ListApprovalChains")
        .WithSummary("List all approval workflow chains")
        .RequireAuthorization("RequireAdmin");

        // POST /api/approval/chains — Create a new approval chain
        group.MapPost("/chains", async (
            [FromBody] CreateChainInput input,
            IRepository<ApprovalChain> repository,
            ITenantIdProvider tenantIdProvider,
            CancellationToken ct) =>
        {
            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant not resolved." });

            if (string.IsNullOrEmpty(input.Name))
                return Results.BadRequest(new { error = "Name is required." });

            var chain = new ApprovalChain
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                Name = input.Name,
                TargetEntityType = input.TargetEntityType ?? "Invoice",
                Description = input.Description,
                CountryCode = input.CountryCode,
                MinTotalAmount = input.MinTotalAmount,
                MaxTotalAmount = input.MaxTotalAmount,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(chain, ct);

            return Results.Created($"/api/approval/chains/{chain.Id}", new
            {
                id = chain.Id,
                name = chain.Name,
                targetEntityType = chain.TargetEntityType,
                isActive = chain.IsActive
            });
        })
        .WithName("CreateApprovalChain")
        .WithSummary("Create a new approval workflow chain")
        .RequireAuthorization("RequireAdmin");

        // DELETE /api/approval/chains/{id} — Deactivate an approval chain
        group.MapDelete("/chains/{id:guid}", async (
            Guid id,
            IRepository<ApprovalChain> repository,
            CancellationToken ct) =>
        {
            var chain = await repository.GetByIdAsync(id, ct);
            if (chain is null) return Results.NotFound();

            chain.IsActive = false;
            await repository.UpdateAsync(chain, ct);

            return Results.NoContent();
        })
        .WithName("DeactivateApprovalChain")
        .WithSummary("Deactivate an approval workflow chain")
        .RequireAuthorization("RequireAdmin");

        // ─── Step Management ─────────────────────────────────

        // POST /api/approval/chains/{chainId}/steps — Add a step to a chain
        group.MapPost("/chains/{chainId:guid}/steps", async (
            Guid chainId,
            [FromBody] CreateStepInput input,
            IRepository<ApprovalChain> chainRepository,
            IRepository<ApprovalStep> stepRepository,
            ITenantIdProvider tenantIdProvider,
            CancellationToken ct) =>
        {
            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant not resolved." });

            var chain = await chainRepository.GetByIdAsync(chainId, ct);
            if (chain is null) return Results.NotFound();

            var step = new ApprovalStep
            {
                Id = Guid.NewGuid(),
                ApprovalChainId = chainId,
                TenantId = tenantId.Value,
                Name = input.Name,
                StepOrder = input.StepOrder,
                RequiredRole = input.RequiredRole,
                AssignedUserId = input.AssignedUserId,
                DeadlineHours = input.DeadlineHours,
                EscalationAction = input.EscalationAction,
                IsOptional = input.IsOptional,
                IsActive = true
            };

            await stepRepository.AddAsync(step, ct);

            return Results.Created($"/api/approval/chains/{chainId}/steps/{step.Id}", new
            {
                id = step.Id,
                name = step.Name,
                stepOrder = step.StepOrder,
                requiredRole = step.RequiredRole,
                deadlineHours = step.DeadlineHours
            });
        })
        .WithName("CreateApprovalStep")
        .WithSummary("Add a step to an approval chain")
        .RequireAuthorization("RequireAdmin");

        // ─── Approval Execution ──────────────────────────────

        // POST /api/approval/invoices/{invoiceId}/start — Start approval for an invoice
        group.MapPost("/invoices/{invoiceId:guid}/start", async (
            Guid invoiceId,
            IWorkflowService workflowService,
            ITenantIdProvider tenantIdProvider,
            CancellationToken ct) =>
        {
            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant not resolved." });

            var result = await workflowService.StartApprovalAsync(invoiceId, tenantId.Value, ct);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
        .WithName("StartApproval")
        .WithSummary("Start the approval workflow for an invoice");

        // POST /api/approval/requests/{requestId}/approve — Approve a step
        group.MapPost("/requests/{requestId:guid}/approve", async (
            Guid requestId,
            [FromBody] ApproveInput input,
            IWorkflowService workflowService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var result = await workflowService.ApproveStepAsync(
                requestId, Guid.Parse(userId), input.Comments, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
        .WithName("ApproveStep")
        .WithSummary("Approve the current approval step");

        // POST /api/approval/requests/{requestId}/reject — Reject a step
        group.MapPost("/requests/{requestId:guid}/reject", async (
            Guid requestId,
            [FromBody] RejectInput input,
            IWorkflowService workflowService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(input.Reason))
                return Results.BadRequest(new { error = "Rejection reason is required." });

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var result = await workflowService.RejectStepAsync(
                requestId, Guid.Parse(userId), input.Reason, ct);

            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
        .WithName("RejectStep")
        .WithSummary("Reject the current approval step");

        // POST /api/approval/escalations/process — Process all escalations
        group.MapPost("/escalations/process", async (
            IWorkflowService workflowService,
            CancellationToken ct) =>
        {
            var count = await workflowService.ProcessEscalationsAsync(ct);
            return Results.Ok(new { escalated = count, message = $"{count} overdue approvals escalated." });
        })
        .WithName("ProcessEscalations")
        .WithSummary("Process all overdue approval escalations")
        .RequireAuthorization("RequireAdmin");

        // GET /api/approval/invoices/{invoiceId}/status — Get chain status
        group.MapGet("/invoices/{invoiceId:guid}/status", async (
            Guid invoiceId,
            IWorkflowService workflowService,
            CancellationToken ct) =>
        {
            var status = await workflowService.GetChainStatusAsync(invoiceId, ct);
            return Results.Ok(status);
        })
        .WithName("GetApprovalStatus")
        .WithSummary("Get the current approval status for an invoice");

        return app;
    }
}
