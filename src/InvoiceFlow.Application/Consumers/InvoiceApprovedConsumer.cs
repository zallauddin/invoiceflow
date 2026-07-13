using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceApprovedEvent — invoice approved by user.</summary>
public sealed class InvoiceApprovedConsumer : IConsumer<InvoiceApprovedEvent>
{
    private readonly ILogger<InvoiceApprovedConsumer> _logger;

    public InvoiceApprovedConsumer(ILogger<InvoiceApprovedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceApprovedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Invoice approved: InvoiceId={InvoiceId}, TenantId={TenantId}, ApprovedBy={ApprovedBy}, Comments={Comments}",
            msg.InvoiceId, msg.TenantId, msg.ApprovedByUserId, msg.Comments);
        return Task.CompletedTask;
    }
}
