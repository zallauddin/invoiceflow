using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles ReminderCreatedEvent — a new payment reminder has been created.</summary>
public sealed class ReminderCreatedConsumer : IConsumer<ReminderCreatedEvent>
{
    private readonly ILogger<ReminderCreatedConsumer> _logger;

    public ReminderCreatedConsumer(ILogger<ReminderCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ReminderCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Reminder created: EventId={EventId}, OccurredAt={OccurredAt}, ReminderId={ReminderId}, TenantId={TenantId}, InvoiceId={InvoiceId}, ReminderLevel={ReminderLevel}, DaysOverdue={DaysOverdue}",
            msg.EventId, msg.OccurredAt, msg.ReminderId, msg.TenantId, msg.InvoiceId, msg.ReminderLevel, msg.DaysOverdue);
        return Task.CompletedTask;
    }
}
