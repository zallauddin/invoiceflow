using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles CreditNoteCancelledEvent — a credit note has been cancelled.</summary>
public sealed class CreditNoteCancelledConsumer : IConsumer<CreditNoteCancelledEvent>
{
    private readonly ILogger<CreditNoteCancelledConsumer> _logger;

    public CreditNoteCancelledConsumer(ILogger<CreditNoteCancelledConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<CreditNoteCancelledEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Credit note cancelled: EventId={EventId}, OccurredAt={OccurredAt}, CreditNoteId={CreditNoteId}, TenantId={TenantId}, Reason={Reason}",
            msg.EventId, msg.OccurredAt, msg.CreditNoteId, msg.TenantId, msg.Reason);
        return Task.CompletedTask;
    }
}
