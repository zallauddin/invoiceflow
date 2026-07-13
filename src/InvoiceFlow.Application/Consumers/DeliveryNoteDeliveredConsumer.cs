using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles DeliveryNoteDeliveredEvent — a delivery note has been marked as delivered.</summary>
public sealed class DeliveryNoteDeliveredConsumer : IConsumer<DeliveryNoteDeliveredEvent>
{
    private readonly ILogger<DeliveryNoteDeliveredConsumer> _logger;

    public DeliveryNoteDeliveredConsumer(ILogger<DeliveryNoteDeliveredConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<DeliveryNoteDeliveredEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Delivery note delivered: EventId={EventId}, OccurredAt={OccurredAt}, DeliveryNoteId={DeliveryNoteId}, TenantId={TenantId}, DeliveredAt={DeliveredAt}, ReceivedBy={ReceivedBy}",
            msg.EventId, msg.OccurredAt, msg.DeliveryNoteId, msg.TenantId, msg.DeliveredAt, msg.ReceivedBy);
        return Task.CompletedTask;
    }
}
