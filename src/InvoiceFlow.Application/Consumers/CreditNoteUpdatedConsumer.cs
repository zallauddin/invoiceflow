using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles CreditNoteUpdatedEvent — an existing credit note has been updated.</summary>
public sealed class CreditNoteUpdatedConsumer : IConsumer<CreditNoteUpdatedEvent>
{
    private readonly ILogger<CreditNoteUpdatedConsumer> _logger;

    public CreditNoteUpdatedConsumer(ILogger<CreditNoteUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<CreditNoteUpdatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Credit note updated: EventId={EventId}, OccurredAt={OccurredAt}, CreditNoteId={CreditNoteId}, TenantId={TenantId}",
            msg.EventId, msg.OccurredAt, msg.CreditNoteId, msg.TenantId);
        return Task.CompletedTask;
    }
}
