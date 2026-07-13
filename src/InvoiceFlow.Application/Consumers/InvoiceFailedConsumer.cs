using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceFailedEvent — processing failure notification.</summary>
public sealed class InvoiceFailedConsumer : IConsumer<InvoiceFailedEvent>
{
    private readonly ILogger<InvoiceFailedConsumer> _logger;

    public InvoiceFailedConsumer(ILogger<InvoiceFailedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceFailedEvent> context)
    {
        var msg = context.Message;
        _logger.LogError(
            "Invoice processing failed: InvoiceId={InvoiceId}, TenantId={TenantId}, Reason={Reason}, StackTrace={StackTrace}",
            msg.InvoiceId, msg.TenantId, msg.FailureReason, msg.StackTrace);
        return Task.CompletedTask;
    }
}
