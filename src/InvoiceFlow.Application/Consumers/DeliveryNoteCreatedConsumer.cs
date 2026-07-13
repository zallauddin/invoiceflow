using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles DeliveryNoteCreatedEvent — a new delivery note has been created.</summary>
public sealed class DeliveryNoteCreatedConsumer : IConsumer<DeliveryNoteCreatedEvent>
{
    private readonly ILogger<DeliveryNoteCreatedConsumer> _logger;

    public DeliveryNoteCreatedConsumer(ILogger<DeliveryNoteCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DeliveryNoteCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Delivery note created: EventId={EventId}, OccurredAt={OccurredAt}, DeliveryNoteId={DeliveryNoteId}, TenantId={TenantId}, DocumentNumber={DocumentNumber}, PurchaseOrderId={PurchaseOrderId}",
            msg.EventId, msg.OccurredAt, msg.DeliveryNoteId, msg.TenantId, msg.DocumentNumber, msg.PurchaseOrderId);
        return Task.CompletedTask;
    }
}
