using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles PurchaseOrderCreatedEvent — a new purchase order has been created.</summary>
public sealed class PurchaseOrderCreatedConsumer : IConsumer<PurchaseOrderCreatedEvent>
{
    private readonly ILogger<PurchaseOrderCreatedConsumer> _logger;

    public PurchaseOrderCreatedConsumer(ILogger<PurchaseOrderCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PurchaseOrderCreatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Purchase order created: EventId={EventId}, OccurredAt={OccurredAt}, PurchaseOrderId={PurchaseOrderId}, TenantId={TenantId}, DocumentNumber={DocumentNumber}",
            msg.EventId, msg.OccurredAt, msg.PurchaseOrderId, msg.TenantId, msg.DocumentNumber);
        return Task.CompletedTask;
    }
}
