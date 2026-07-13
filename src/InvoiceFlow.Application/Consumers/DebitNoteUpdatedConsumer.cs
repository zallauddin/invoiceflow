using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles DebitNoteUpdatedEvent — an existing debit note has been updated.</summary>
public sealed class DebitNoteUpdatedConsumer : IConsumer<DebitNoteUpdatedEvent>
{
    private readonly ILogger<DebitNoteUpdatedConsumer> _logger;

    public DebitNoteUpdatedConsumer(ILogger<DebitNoteUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DebitNoteUpdatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Debit note updated: EventId={EventId}, OccurredAt={OccurredAt}, DebitNoteId={DebitNoteId}, TenantId={TenantId}",
            msg.EventId, msg.OccurredAt, msg.DebitNoteId, msg.TenantId);
        return Task.CompletedTask;
    }
}
