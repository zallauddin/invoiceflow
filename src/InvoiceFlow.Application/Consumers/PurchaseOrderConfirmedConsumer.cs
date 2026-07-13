using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles PurchaseOrderConfirmedEvent — a purchase order has been confirmed.</summary>
public sealed class PurchaseOrderConfirmedConsumer : IConsumer<PurchaseOrderConfirmedEvent>
{
    private readonly ILogger<PurchaseOrderConfirmedConsumer> _logger;

    public PurchaseOrderConfirmedConsumer(ILogger<PurchaseOrderConfirmedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PurchaseOrderConfirmedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Purchase order confirmed: EventId={EventId}, OccurredAt={OccurredAt}, PurchaseOrderId={PurchaseOrderId}, TenantId={TenantId}, ConfirmedBy={ConfirmedBy}",
            msg.EventId, msg.OccurredAt, msg.PurchaseOrderId, msg.TenantId, msg.ConfirmedBy);
        return Task.CompletedTask;
    }
}
