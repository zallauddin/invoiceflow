using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>
/// Fans out a batch extraction request into individual <see cref="ExtractInvoiceCommand"/>
/// messages with a small inter-publish delay to avoid overwhelming downstream consumers.
/// </summary>
public sealed class BatchExtractInvoicesConsumer : IConsumer<BatchExtractInvoicesCommand>
{
    private static readonly TimeSpan InterPublishDelay = TimeSpan.FromMilliseconds(100);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BatchExtractInvoicesConsumer> _logger;

    public BatchExtractInvoicesConsumer(
        IPublishEndpoint publishEndpoint,
        ILogger<BatchExtractInvoicesConsumer> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BatchExtractInvoicesCommand> context)
    {
        var msg = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Batch extraction started: Count={Count}, TenantId={TenantId}, CorrelationId={CorrelationId}",
            msg.InvoiceIds.Length, msg.TenantId, msg.CorrelationId);

        for (var index = 0; index < msg.InvoiceIds.Length; index++)
        {
            var invoiceId = msg.InvoiceIds[index];

            _logger.LogDebug(
                "Publishing extraction command {Index}/{Total}: InvoiceId={InvoiceId}, CorrelationId={CorrelationId}",
                index + 1, msg.InvoiceIds.Length, invoiceId, msg.CorrelationId);

            await _publishEndpoint.Publish(new ExtractInvoiceCommand
            {
                InvoiceId = invoiceId,
                TenantId = msg.TenantId,
                CorrelationId = msg.CorrelationId,
                Priority = msg.Priority
            }, cancellationToken);

            if (index < msg.InvoiceIds.Length - 1)
            {
                await Task.Delay(InterPublishDelay, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Batch extraction commands published: Count={Count}, CorrelationId={CorrelationId}",
            msg.InvoiceIds.Length, msg.CorrelationId);
    }
}
