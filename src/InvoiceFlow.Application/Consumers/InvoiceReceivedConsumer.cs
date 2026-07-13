using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceReceivedEvent — initial ingestion acknowledgment.</summary>
public sealed class InvoiceReceivedConsumer : IConsumer<InvoiceReceivedEvent>
{
    private readonly ILogger<InvoiceReceivedConsumer> _logger;

    public InvoiceReceivedConsumer(ILogger<InvoiceReceivedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceReceivedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Invoice received: InvoiceId={InvoiceId}, TenantId={TenantId}, Source={Source}, FileName={FileName}",
            msg.InvoiceId, msg.TenantId, msg.Source, msg.FileName);
        return Task.CompletedTask;
    }
}
