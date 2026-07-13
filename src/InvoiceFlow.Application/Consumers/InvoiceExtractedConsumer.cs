using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>Handles InvoiceExtractedEvent — OCR/extraction completed.</summary>
public sealed class InvoiceExtractedConsumer : IConsumer<InvoiceExtractedEvent>
{
    private readonly ILogger<InvoiceExtractedConsumer> _logger;

    public InvoiceExtractedConsumer(ILogger<InvoiceExtractedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<InvoiceExtractedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Invoice extracted: InvoiceId={InvoiceId}, TenantId={TenantId}, Method={Method}, Confidence={Confidence:P1}",
            msg.InvoiceId, msg.TenantId, msg.ExtractionMethod, msg.Confidence);
        return Task.CompletedTask;
    }
}
