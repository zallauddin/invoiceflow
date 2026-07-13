using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceRejectedEvent — invoice rejected by user.</summary>
public sealed class InvoiceRejectedConsumer : IConsumer<InvoiceRejectedEvent>
{
    private readonly ILogger<InvoiceRejectedConsumer> _logger;

    public InvoiceRejectedConsumer(ILogger<InvoiceRejectedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceRejectedEvent> context)
    {
        var msg = context.Message;
        _logger.LogWarning(
            "Invoice rejected: InvoiceId={InvoiceId}, TenantId={TenantId}, RejectedBy={RejectedBy}, Reason={Reason}",
            msg.InvoiceId, msg.TenantId, msg.RejectedByUserId, msg.Reason);
        return Task.CompletedTask;
    }
}
