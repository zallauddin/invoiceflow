using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Delivery note endpoints: list, get, create, update, mark delivered, and soft-delete.
/// </summary>
public static class DeliveryNoteEndpoints
{
    /// <summary>Input model for creating or updating a delivery note.</summary>
    private sealed record DeliveryNoteInput(
        string DocumentNumber,
        DateTime DocumentDate,
        string IssuerName,
        string RecipientName,
        string Currency,
        decimal Subtotal,
        decimal TaxAmount,
        decimal TotalAmount,
        Guid? PurchaseOrderId,
        string? DeliveryAddress,
        string? CarrierName,
        string? TrackingNumber);

    /// <summary>Input model for marking a delivery note as delivered.</summary>
    private sealed record DeliverInput(
        DateTime DeliveredAt,
        string? ReceivedBy);

    public static WebApplication MapDeliveryNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/delivery-notes")
            .WithTags("DeliveryNotes")
            .RequireAuthorization();

        // GET /api/delivery-notes — List all delivery notes
        group.MapGet("/", async (
            IRepository<DeliveryNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 1000, cancellationToken);
            return Results.Ok(entities);
        })
        .WithName("ListDeliveryNotes")
        .WithSummary("List all delivery notes for the current tenant");

        // GET /api/delivery-notes/{id} — Get delivery note by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<DeliveryNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetDeliveryNote")
        .WithSummary("Get a delivery note by its ID");

        // POST /api/delivery-notes — Create a new delivery note
        group.MapPost("/", async (
            [FromBody] DeliveryNoteInput input,
            IRepository<DeliveryNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = new DeliveryNote
            {
                Id = Guid.NewGuid(),
                DocumentNumber = input.DocumentNumber,
                DocumentDate = input.DocumentDate,
                IssuerName = input.IssuerName,
                RecipientName = input.RecipientName,
                Currency = input.Currency,
                Subtotal = input.Subtotal,
                TaxAmount = input.TaxAmount,
                TotalAmount = input.TotalAmount,
                PurchaseOrderId = input.PurchaseOrderId,
                DeliveryAddress = input.DeliveryAddress,
                CarrierName = input.CarrierName,
                TrackingNumber = input.TrackingNumber,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new DeliveryNoteCreatedEvent
            {
                DeliveryNoteId = entity.Id,
                TenantId = entity.TenantId,
                DocumentNumber = entity.DocumentNumber,
                PurchaseOrderId = entity.PurchaseOrderId
            }, cancellationToken);

            return Results.Created($"/api/delivery-notes/{entity.Id}", entity);
        })
        .WithName("CreateDeliveryNote")
        .WithSummary("Create a new delivery note");

        // PUT /api/delivery-notes/{id} — Update a delivery note
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] DeliveryNoteInput input,
            IRepository<DeliveryNote> repository,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.DocumentNumber = input.DocumentNumber;
            entity.DocumentDate = input.DocumentDate;
            entity.IssuerName = input.IssuerName;
            entity.RecipientName = input.RecipientName;
            entity.Currency = input.Currency;
            entity.Subtotal = input.Subtotal;
            entity.TaxAmount = input.TaxAmount;
            entity.TotalAmount = input.TotalAmount;
            entity.PurchaseOrderId = input.PurchaseOrderId;
            entity.DeliveryAddress = input.DeliveryAddress;
            entity.CarrierName = input.CarrierName;
            entity.TrackingNumber = input.TrackingNumber;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateDeliveryNote")
        .WithSummary("Update an existing delivery note");

        // PUT /api/delivery-notes/{id}/deliver — Mark delivery note as delivered
        group.MapPut("/{id:guid}/deliver", async (
            Guid id,
            [FromBody] DeliverInput input,
            IRepository<DeliveryNote> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.DeliveredAt = input.DeliveredAt;
            entity.ReceivedBy = input.ReceivedBy;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new DeliveryNoteDeliveredEvent
            {
                DeliveryNoteId = entity.Id,
                TenantId = entity.TenantId,
                DeliveredAt = input.DeliveredAt,
                ReceivedBy = input.ReceivedBy
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeliverDeliveryNote")
        .WithSummary("Mark a delivery note as delivered");

        // DELETE /api/delivery-notes/{id} — Soft-delete a delivery note
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IRepository<DeliveryNote> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteDeliveryNote")
        .WithSummary("Soft-delete a delivery note");

        return app;
    }
}
