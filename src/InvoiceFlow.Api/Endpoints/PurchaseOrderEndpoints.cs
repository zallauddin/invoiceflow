using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Purchase order endpoints: list, get, create, update, confirm, and soft-delete.
/// </summary>
public static class PurchaseOrderEndpoints
{
    /// <summary>Input model for creating or updating a purchase order.</summary>
    private sealed record PurchaseOrderInput(
        string DocumentNumber,
        DateTime DocumentDate,
        DateTime? DueDate,
        string IssuerName,
        string RecipientName,
        string Currency,
        decimal Subtotal,
        decimal TaxAmount,
        decimal TotalAmount,
        DateTime? ExpectedDeliveryDate,
        string? DeliveryAddress,
        string? PaymentTerms,
        string? Incoterms,
        string? ShipToName,
        string? ShipToAddress,
        string? BillToName,
        string? BillToAddress,
        string? ContactName,
        string? ContactEmail,
        string? ContactPhone);

    public static WebApplication MapPurchaseOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase-orders")
            .WithTags("PurchaseOrders")
            .RequireAuthorization();

        // GET /api/purchase-orders — List all purchase orders
        group.MapGet("/", async (
            IRepository<PurchaseOrder> repository,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 1000, cancellationToken);
            return Results.Ok(entities);
        })
        .WithName("ListPurchaseOrders")
        .WithSummary("List all purchase orders for the current tenant");

        // GET /api/purchase-orders/{id} — Get purchase order by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<PurchaseOrder> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetPurchaseOrder")
        .WithSummary("Get a purchase order by its ID");

        // POST /api/purchase-orders — Create a new purchase order
        group.MapPost("/", async (
            [FromBody] PurchaseOrderInput input,
            IRepository<PurchaseOrder> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = new PurchaseOrder
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
                ExpectedDeliveryDate = input.ExpectedDeliveryDate,
                DeliveryAddress = input.DeliveryAddress,
                PaymentTerms = input.PaymentTerms,
                Incoterms = input.Incoterms,
                ShipToName = input.ShipToName,
                ShipToAddress = input.ShipToAddress,
                BillToName = input.BillToName,
                BillToAddress = input.BillToAddress,
                ContactName = input.ContactName,
                ContactEmail = input.ContactEmail,
                ContactPhone = input.ContactPhone,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new PurchaseOrderCreatedEvent
            {
                PurchaseOrderId = entity.Id,
                TenantId = entity.TenantId,
                DocumentNumber = entity.DocumentNumber
            }, cancellationToken);

            return Results.Created($"/api/purchase-orders/{entity.Id}", entity);
        })
        .WithName("CreatePurchaseOrder")
        .WithSummary("Create a new purchase order");

        // PUT /api/purchase-orders/{id} — Update a purchase order
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] PurchaseOrderInput input,
            IRepository<PurchaseOrder> repository,
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
            entity.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
            entity.DeliveryAddress = input.DeliveryAddress;
            entity.PaymentTerms = input.PaymentTerms;
            entity.Incoterms = input.Incoterms;
            entity.ShipToName = input.ShipToName;
            entity.ShipToAddress = input.ShipToAddress;
            entity.BillToName = input.BillToName;
            entity.BillToAddress = input.BillToAddress;
            entity.ContactName = input.ContactName;
            entity.ContactEmail = input.ContactEmail;
            entity.ContactPhone = input.ContactPhone;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new PurchaseOrderUpdatedEvent
            {
                PurchaseOrderId = entity.Id,
                TenantId = entity.TenantId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdatePurchaseOrder")
        .WithSummary("Update an existing purchase order");

        // PUT /api/purchase-orders/{id}/confirm — Confirm a purchase order
        group.MapPut("/{id:guid}/confirm", async (
            Guid id,
            IRepository<PurchaseOrder> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new PurchaseOrderConfirmedEvent
            {
                PurchaseOrderId = entity.Id,
                TenantId = entity.TenantId
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("ConfirmPurchaseOrder")
        .WithSummary("Confirm a purchase order");

        // DELETE /api/purchase-orders/{id} — Soft-delete a purchase order
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IRepository<PurchaseOrder> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeletePurchaseOrder")
        .WithSummary("Soft-delete a purchase order");

        return app;
    }
}
