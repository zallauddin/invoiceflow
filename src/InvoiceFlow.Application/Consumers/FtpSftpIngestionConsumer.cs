using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>
/// Consumes <see cref="TriggerFtpSftpIngestionCommand"/> to poll the configured FTP/SFTP remote
/// directory for matching invoice files. Each file is downloaded, stored in object storage,
/// linked to a Document and Invoice entity, and dispatched for extraction processing.
/// </summary>
public sealed class FtpSftpIngestionConsumer : IConsumer<TriggerFtpSftpIngestionCommand>
{
    private readonly IFtpSftpIngestionService _ftpSftpIngestionService;
    private readonly ILogger<FtpSftpIngestionConsumer> _logger;

    public FtpSftpIngestionConsumer(
        IFtpSftpIngestionService ftpSftpIngestionService,
        ILogger<FtpSftpIngestionConsumer> logger)
    {
        _ftpSftpIngestionService = ftpSftpIngestionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TriggerFtpSftpIngestionCommand> context)
    {
        var msg = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "FTP/SFTP ingestion triggered: TenantId={TenantId}, CorrelationId={CorrelationId}",
            msg.TenantId, msg.CorrelationId);

        try
        {
            var result = await _ftpSftpIngestionService.PollAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "FTP/SFTP ingestion completed: Processed={Processed}, Failed={Failed}, Documents={DocumentCount}, CorrelationId={CorrelationId}",
                    result.ProcessedCount, result.FailedCount, result.CreatedDocuments.Count, msg.CorrelationId);
            }
            else
            {
                _logger.LogWarning(
                    "FTP/SFTP ingestion completed with errors: Processed={Processed}, Failed={Failed}, Error={Error}, CorrelationId={CorrelationId}",
                    result.ProcessedCount, result.FailedCount, result.ErrorMessage, msg.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FTP/SFTP ingestion failed with exception: TenantId={TenantId}, CorrelationId={CorrelationId}, Error={Error}",
                msg.TenantId, msg.CorrelationId, ex.Message);
            throw;
        }
    }
}
