using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles ReminderEscalatedEvent — a payment reminder has been escalated to a higher level.</summary>
public sealed class ReminderEscalatedConsumer : IConsumer<ReminderEscalatedEvent>
{
    private readonly ILogger<ReminderEscalatedConsumer> _logger;

    public ReminderEscalatedConsumer(ILogger<ReminderEscalatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ReminderEscalatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Reminder escalated: EventId={EventId}, OccurredAt={OccurredAt}, ReminderId={ReminderId}, TenantId={TenantId}, InvoiceId={InvoiceId}, NewLevel={NewLevel}",
            msg.EventId, msg.OccurredAt, msg.ReminderId, msg.TenantId, msg.InvoiceId, msg.NewLevel);
        return Task.CompletedTask;
    }
}
