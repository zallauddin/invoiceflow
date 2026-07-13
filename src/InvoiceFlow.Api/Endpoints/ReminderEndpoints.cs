using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// Reminder endpoints: list, get, create, send, escalate, and soft-delete payment reminders.
/// </summary>
public static class ReminderEndpoints
{
    /// <summary>Input model for creating a reminder.</summary>
    private sealed record ReminderInput(
        string DocumentNumber,
        Guid InvoiceId,
        int ReminderLevel,
        int DaysOverdue,
        decimal? ReminderFee,
        DateTime? DueDate);

    public static WebApplication MapReminderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reminders")
            .WithTags("Reminders")
            .RequireAuthorization();

        // GET /api/reminders — List all reminders
        group.MapGet("/", async (
            IRepository<Reminder> repository,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 1000, cancellationToken);
            return Results.Ok(entities);
        })
        .WithName("ListReminders")
        .WithSummary("List all reminders for the current tenant");

        // GET /api/reminders/{id} — Get reminder by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<Reminder> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetReminder")
        .WithSummary("Get a reminder by its ID");

        // POST /api/reminders — Create a new reminder
        group.MapPost("/", async (
            [FromBody] ReminderInput input,
            IRepository<Reminder> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(input.DocumentNumber))
                return Results.BadRequest("DocumentNumber is required.");

            var entity = new Reminder
            {
                Id = Guid.NewGuid(),
                DocumentNumber = input.DocumentNumber,
                InvoiceId = input.InvoiceId,
                ReminderLevel = input.ReminderLevel,
                DaysOverdue = input.DaysOverdue,
                ReminderFee = input.ReminderFee,
                DueDate = input.DueDate,
                CreatedAt = DateTime.UtcNow
            };

            await repository.AddAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new ReminderCreatedEvent
            {
                ReminderId = entity.Id,
                TenantId = entity.TenantId,
                InvoiceId = entity.InvoiceId,
                ReminderLevel = entity.ReminderLevel,
                DaysOverdue = entity.DaysOverdue
            }, cancellationToken);

            return Results.Created($"/api/reminders/{entity.Id}", entity);
        })
        .WithName("CreateReminder")
        .WithSummary("Create a new payment reminder");

        // PUT /api/reminders/{id}/send — Mark reminder as sent
        group.MapPut("/{id:guid}/send", async (
            Guid id,
            IRepository<Reminder> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            var sentAt = DateTime.UtcNow;
            entity.SentAt = sentAt;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new ReminderSentEvent
            {
                ReminderId = entity.Id,
                TenantId = entity.TenantId,
                InvoiceId = entity.InvoiceId,
                SentAt = sentAt,
                ReminderLevel = entity.ReminderLevel
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("SendReminder")
        .WithSummary("Mark a reminder as sent to the recipient");

        // PUT /api/reminders/{id}/escalate — Escalate reminder to next level
        group.MapPut("/{id:guid}/escalate", async (
            Guid id,
            IRepository<Reminder> repository,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            var newLevel = entity.ReminderLevel + 1;
            entity.ReminderLevel = newLevel;
            entity.UpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            await publishEndpoint.Publish(new ReminderEscalatedEvent
            {
                ReminderId = entity.Id,
                TenantId = entity.TenantId,
                InvoiceId = entity.InvoiceId,
                NewLevel = newLevel
            }, cancellationToken);

            return Results.NoContent();
        })
        .WithName("EscalateReminder")
        .WithSummary("Escalate a reminder to the next level");

        // DELETE /api/reminders/{id} — Soft-delete a reminder
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IRepository<Reminder> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            if (entity is null) return Results.NotFound();

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;

            await repository.UpdateAsync(entity, cancellationToken);

            return Results.NoContent();
        })
        .WithName("DeleteReminder")
        .WithSummary("Soft-delete a reminder");

        return app;
    }
}
