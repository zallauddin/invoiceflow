using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Credit note endpoints: list, get, create, update, and soft-delete credit notes.
/// </summary>
public static class CreditNoteEndpoints
{
    /// <summary>Input model for creating or updating a credit note.</summary>
    private sealed record CreditNoteInput(
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

    public static WebApplication MapCreditNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/credit-notes")
            .WithTags("CreditNotes")
            .RequireAuthorization();

        // GET /api/credit-notes — List all credit notes
        group.MapGet("/", async (
            IRepository<CreditNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 1000, cancellationToken);
            return Results.Ok(entities);
        })
        .WithName("ListCreditNotes")
        .WithSummary("List all credit notes for the current tenant");

        // GET /api/credit-notes/{id} — Get credit note by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<CreditNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetCreditNote")
        .WithSummary("Get a credit note by its ID");

        // POST /api/credit-notes — Create a new credit note
        group.MapPost("/", async (
            [FromBody] CreditNoteInput input,
            IRepository<CreditNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = new CreditNote
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

            await publishEndpoint.Publish(new CreditNoteCreatedEvent
            {
                CreditNoteId = entity.Id,
                TenantId = entity.TenantId,
                DocumentNumber = entity.DocumentNumber,
                OriginalInvoiceId = entity.OriginalInvoiceId
            }, cancellationToken);

            return Results.Created($"/api/credit-notes/{entity.Id}", entity);
        })
        .WithName("CreateCreditNote")
        .WithSummary("Create a new credit note");

        // PUT /api/credit-notes/{id} — Update a credit note
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] CreditNoteInput input,
            IRepository<CreditNote> repository,
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

            await publishEndpoint.Publish(new CreditNoteUpdatedEvent
            {
                CreditNoteId = entity.Id,
                TenantId = entity.TenantId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateCreditNote")
        .WithSummary("Update an existing credit note");

        // DELETE /api/credit-notes/{id} — Soft-delete a credit note
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IRepository<CreditNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new CreditNoteCancelledEvent
            {
                CreditNoteId = entity.Id,
                TenantId = entity.TenantId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteCreditNote")
        .WithSummary("Soft-delete a credit note");

        return app;
    }
}
