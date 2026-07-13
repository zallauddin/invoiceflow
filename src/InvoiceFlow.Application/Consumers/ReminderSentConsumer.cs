using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles ReminderSentEvent — a payment reminder has been sent.</summary>
public sealed class ReminderSentConsumer : IConsumer<ReminderSentEvent>
{
    private readonly ILogger<ReminderSentConsumer> _logger;

    public ReminderSentConsumer(ILogger<ReminderSentConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ReminderSentEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Reminder sent: EventId={EventId}, OccurredAt={OccurredAt}, ReminderId={ReminderId}, TenantId={TenantId}, InvoiceId={InvoiceId}, SentAt={SentAt}, ReminderLevel={ReminderLevel}",
            msg.EventId, msg.OccurredAt, msg.ReminderId, msg.TenantId, msg.InvoiceId, msg.SentAt, msg.ReminderLevel);
        return Task.CompletedTask;
    }
}
