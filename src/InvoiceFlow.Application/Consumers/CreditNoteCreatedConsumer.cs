using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles CreditNoteCreatedEvent — a new credit note has been created.</summary>
public sealed class CreditNoteCreatedConsumer : IConsumer<CreditNoteCreatedEvent>
{
    private readonly ILogger<CreditNoteCreatedConsumer> _logger;

    public CreditNoteCreatedConsumer(ILogger<CreditNoteCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<CreditNoteCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Credit note created: EventId={EventId}, OccurredAt={OccurredAt}, CreditNoteId={CreditNoteId}, TenantId={TenantId}, DocumentNumber={DocumentNumber}, OriginalInvoiceId={OriginalInvoiceId}",
            msg.EventId, msg.OccurredAt, msg.CreditNoteId, msg.TenantId, msg.DocumentNumber, msg.OriginalInvoiceId);
        return Task.CompletedTask;
    }
}
