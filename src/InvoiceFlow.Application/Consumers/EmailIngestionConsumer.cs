using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>
/// Consumes <see cref="TriggerEmailIngestionCommand"/> to poll the configured IMAP mailbox
/// for unread messages with invoice attachments. Each attachment is stored in object storage,
/// linked to a Document and Invoice entity, and dispatched for extraction processing.
/// </summary>
public sealed class EmailIngestionConsumer : IConsumer<TriggerEmailIngestionCommand>
{
    private readonly IEmailIngestionService _emailIngestionService;
    private readonly ILogger<EmailIngestionConsumer> _logger;

    public EmailIngestionConsumer(
        IEmailIngestionService emailIngestionService,
        ILogger<EmailIngestionConsumer> logger)
    {
        _emailIngestionService = emailIngestionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TriggerEmailIngestionCommand> context)
    {
        var msg = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Email ingestion triggered: TenantId={TenantId}, CorrelationId={CorrelationId}",
            msg.TenantId, msg.CorrelationId);

        try
        {
            var result = await _emailIngestionService.PollEmailsAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Email ingestion completed: Processed={Processed}, Failed={Failed}, Documents={DocumentCount}, CorrelationId={CorrelationId}",
                    result.ProcessedCount, result.FailedCount, result.CreatedDocuments.Count, msg.CorrelationId);
            }
            else
            {
                _logger.LogWarning(
                    "Email ingestion completed with errors: Processed={Processed}, Failed={Failed}, Error={Error}, CorrelationId={CorrelationId}",
                    result.ProcessedCount, result.FailedCount, result.ErrorMessage, msg.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Email ingestion failed with exception: TenantId={TenantId}, CorrelationId={CorrelationId}, Error={Error}",
                msg.TenantId, msg.CorrelationId, ex.Message);
            throw;
        }
    }
}
