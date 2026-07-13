using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles DebitNoteCreatedEvent — a new debit note has been created.</summary>
public sealed class DebitNoteCreatedConsumer : IConsumer<DebitNoteCreatedEvent>
{
    private readonly ILogger<DebitNoteCreatedConsumer> _logger;

    public DebitNoteCreatedConsumer(ILogger<DebitNoteCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DebitNoteCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Debit note created: EventId={EventId}, OccurredAt={OccurredAt}, DebitNoteId={DebitNoteId}, TenantId={TenantId}, DocumentNumber={DocumentNumber}, OriginalInvoiceId={OriginalInvoiceId}",
            msg.EventId, msg.OccurredAt, msg.DebitNoteId, msg.TenantId, msg.DocumentNumber, msg.OriginalInvoiceId);
        return Task.CompletedTask;
    }
}
