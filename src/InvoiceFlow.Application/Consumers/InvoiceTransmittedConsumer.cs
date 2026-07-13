using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceTransmittedEvent — invoice transmitted to external system.</summary>
public sealed class InvoiceTransmittedConsumer : IConsumer<InvoiceTransmittedEvent>
{
    private readonly ILogger<InvoiceTransmittedConsumer> _logger;

    public InvoiceTransmittedConsumer(ILogger<InvoiceTransmittedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceTransmittedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Invoice transmitted: InvoiceId={InvoiceId}, TenantId={TenantId}, TransmissionId={TransmissionId}",
            msg.InvoiceId, msg.TenantId, msg.TransmissionId);
        return Task.CompletedTask;
    }
}
