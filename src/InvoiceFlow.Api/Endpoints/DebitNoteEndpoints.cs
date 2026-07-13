using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Debit note endpoints: list, get, create, update, and soft-delete debit notes.
/// </summary>
public static class DebitNoteEndpoints
{
    /// <summary>Input model for creating or updating a debit note.</summary>
    private sealed record DebitNoteInput(
        string DocumentNumber,
        DateTime DocumentDate,
        DateTime? DueDate,
        string IssuerName,
        string RecipientName,
        string Currency,
        decimal Subtotal,
        decimal TaxAmount,
        decimal TotalAmount,
        Guid? OriginalInvoiceId,
        string? Reason);

    public static WebApplication MapDebitNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/debit-notes")
            .WithTags("DebitNotes")
            .RequireAuthorization();

        // GET /api/debit-notes — List all debit notes
        group.MapGet("/", async (
            IRepository<DebitNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 1000, cancellationToken);
            return Results.Ok(entities);
        })
        .WithName("ListDebitNotes")
        .WithSummary("List all debit notes for the current tenant");

        // GET /api/debit-notes/{id} — Get debit note by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<DebitNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetDebitNote")
        .WithSummary("Get a debit note by its ID");

        // POST /api/debit-notes — Create a new debit note
        group.MapPost("/", async (
            [FromBody] DebitNoteInput input,
            IRepository<DebitNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = new DebitNote
            {
                Id = Guid.NewGuid(),
                DocumentNumber = input.DocumentNumber,
                DocumentDate = input.DocumentDate,
                DueDate = input.DueDate,
                IssuerName = input.IssuerName,
                RecipientName = input.RecipientName,
                Currency = input.Currency,
                Subtotal = input.Subtotal,
                TaxAmount = input.TaxAmount,
                TotalAmount = input.TotalAmount,
                OriginalInvoiceId = input.OriginalInvoiceId,
                Reason = input.Reason,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new DebitNoteCreatedEvent
            {
                DebitNoteId = entity.Id,
                TenantId = entity.TenantId,
                DocumentNumber = entity.DocumentNumber,
                OriginalInvoiceId = entity.OriginalInvoiceId
            }, cancellationToken);

            return Results.Created($"/api/debit-notes/{entity.Id}", entity);
        })
        .WithName("CreateDebitNote")
        .WithSummary("Create a new debit note");

        // PUT /api/debit-notes/{id} — Update a debit note
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] DebitNoteInput input,
            IRepository<DebitNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.DocumentNumber = input.DocumentNumber;
            entity.DocumentDate = input.DocumentDate;
            entity.DueDate = input.DueDate;
            entity.IssuerName = input.IssuerName;
            entity.RecipientName = input.RecipientName;
            entity.Currency = input.Currency;
            entity.Subtotal = input.Subtotal;
            entity.TaxAmount = input.TaxAmount;
            entity.TotalAmount = input.TotalAmount;
            entity.OriginalInvoiceId = input.OriginalInvoiceId;
            entity.Reason = input.Reason;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new DebitNoteUpdatedEvent
            {
                DebitNoteId = entity.Id,
                TenantId = entity.TenantId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateDebitNote")
        .WithSummary("Update an existing debit note");

        // DELETE /api/debit-notes/{id} — Soft-delete a debit note
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IRepository<DebitNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteDebitNote")
        .WithSummary("Soft-delete a debit note");

        return app;
    }
}
