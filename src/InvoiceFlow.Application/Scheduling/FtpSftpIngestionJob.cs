using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using Quartz;

namespace InvoiceFlow.Application.Scheduling;

/// <summary>
/// Quartz.NET job that periodically publishes <see cref="TriggerFtpSftpIngestionCommand"/>
/// to the MassTransit bus, triggering the FTP/SFTP ingestion consumer to poll the remote directory.
/// Schedule is driven by the <c>IngestionScheduling:FtpSftpCronExpression</c> configuration value.
/// </summary>
public sealed class FtpSftpIngestionJob : IJob
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FtpSftpIngestionJob> _logger;

    public FtpSftpIngestionJob(
        IPublishEndpoint publishEndpoint,
        ILogger<FtpSftpIngestionJob> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var tenantId = context.MergedJobDataMap.GetString("TenantId");
        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "FTP/SFTP ingestion job firing: TenantId={TenantId}, CorrelationId={CorrelationId}, ScheduledFireTime={FireTime}",
            tenantId, correlationId, context.ScheduledFireTimeUtc);

        try
        {
            var command = new TriggerFtpSftpIngestionCommand
            {
                TenantId = Guid.TryParse(tenantId, out var parsed) ? parsed : Guid.Empty,
                CorrelationId = correlationId
            };

            await _publishEndpoint.Publish(command, context.CancellationToken);

            _logger.LogInformation(
                "FTP/SFTP ingestion command published: CorrelationId={CorrelationId}",
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish FTP/SFTP ingestion command: CorrelationId={CorrelationId}",
                correlationId);
            throw;
        }
    }
}
