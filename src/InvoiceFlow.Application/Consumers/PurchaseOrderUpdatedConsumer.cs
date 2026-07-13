using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles PurchaseOrderUpdatedEvent — an existing purchase order has been updated.</summary>
public sealed class PurchaseOrderUpdatedConsumer : IConsumer<PurchaseOrderUpdatedEvent>
{
    private readonly ILogger<PurchaseOrderUpdatedConsumer> _logger;

    public PurchaseOrderUpdatedConsumer(ILogger<PurchaseOrderUpdatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PurchaseOrderUpdatedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Purchase order updated: EventId={EventId}, OccurredAt={OccurredAt}, PurchaseOrderId={PurchaseOrderId}, TenantId={TenantId}",
            msg.EventId, msg.OccurredAt, msg.PurchaseOrderId, msg.TenantId);
        return Task.CompletedTask;
    }
}
