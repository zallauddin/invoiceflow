using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using Quartz;

namespace InvoiceFlow.Application.Scheduling;

/// <summary>
/// Quartz.NET job that periodically publishes <see cref="TriggerEmailIngestionCommand"/>
/// to the MassTransit bus, triggering the email ingestion consumer to poll the IMAP mailbox.
/// Schedule is driven by the <c>IngestionScheduling:EmailCronExpression</c> configuration value.
/// </summary>
public sealed class EmailIngestionJob : IJob
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<EmailIngestionJob> _logger;

    public EmailIngestionJob(
        IPublishEndpoint publishEndpoint,
        ILogger<EmailIngestionJob> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var tenantId = context.MergedJobDataMap.GetString("TenantId");
        var correlationId = Guid.NewGuid();

        _logger.LogInformation(
            "Email ingestion job firing: TenantId={TenantId}, CorrelationId={CorrelationId}, ScheduledFireTime={FireTime}",
            tenantId, correlationId, context.ScheduledFireTimeUtc);

        try
        {
            var command = new TriggerEmailIngestionCommand
            {
                TenantId = Guid.TryParse(tenantId, out var parsed) ? parsed : Guid.Empty,
                CorrelationId = correlationId
            };

            await _publishEndpoint.Publish(command, context.CancellationToken);

            _logger.LogInformation(
                "Email ingestion command published: CorrelationId={CorrelationId}",
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish email ingestion command: CorrelationId={CorrelationId}",
                correlationId);
            throw;
        }
    }
}
