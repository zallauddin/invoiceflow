using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceCompliantEvent — compliance check passed.</summary>
public sealed class InvoiceCompliantConsumer : IConsumer<InvoiceCompliantEvent>
{
    private readonly ILogger<InvoiceCompliantConsumer> _logger;

    public InvoiceCompliantConsumer(ILogger<InvoiceCompliantConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceCompliantEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Invoice compliant: InvoiceId={InvoiceId}, TenantId={TenantId}, Model={Model}, ComplianceId={ComplianceId}",
            msg.InvoiceId, msg.TenantId, msg.ComplianceModel, msg.ComplianceId);
        return Task.CompletedTask;
    }
}
