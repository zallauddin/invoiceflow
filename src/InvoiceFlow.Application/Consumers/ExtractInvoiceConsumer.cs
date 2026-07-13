using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Application.Consumers;

/// <summary>
/// Orchestrates single-invoice data extraction: loads entities, invokes the
/// extraction pipeline, persists results, and publishes lifecycle events.
/// Retries up to 3 times with exponential back-off on transient failures.
/// </summary>
public sealed class ExtractInvoiceConsumer : IConsumer<ExtractInvoiceCommand>
{
    private const int MaxRetryCount = 3;

    private readonly IExtractionOrchestrator _extractionOrchestrator;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly IRepository<Document> _documentRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ExtractInvoiceConsumer> _logger;

    public ExtractInvoiceConsumer(
        IExtractionOrchestrator extractionOrchestrator,
        IRepository<Invoice> invoiceRepository,
        IRepository<Document> documentRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<ExtractInvoiceConsumer> logger)
    {
        _extractionOrchestrator = extractionOrchestrator;
        _invoiceRepository = invoiceRepository;
        _documentRepository = documentRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExtractInvoiceCommand> context)
    {
        var msg = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Starting extraction for InvoiceId={InvoiceId}, RetryCount={RetryCount}",
            msg.InvoiceId, msg.RetryCount);

        var invoice = await _invoiceRepository.GetByIdAsync(msg.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            _logger.LogError(
                "Invoice not found: InvoiceId={InvoiceId}. Aborting extraction.",
                msg.InvoiceId);
            return;
        }

        var document = await _documentRepository.GetByIdAsync(msg.DocumentId, cancellationToken);
        if (document is null)
        {
            _logger.LogError(
                "Document not found: DocumentId={DocumentId} for InvoiceId={InvoiceId}. Aborting extraction.",
                msg.DocumentId, msg.InvoiceId);
            return;
        }

        // Publish initial progress indicator
        await _publishEndpoint.Publish(new ExtractionProgressEvent
        {
            InvoiceId = msg.InvoiceId,
            TenantId = msg.TenantId,
            Stage = "OCR",
            Progress = 10,
            Message = "Extraction pipeline started"
        }, cancellationToken);

        try
        {
            var result = await _extractionOrchestrator.ProcessAsync(document, invoice, cancellationToken);

            if (result.Success)
            {
                // Update invoice with extraction metadata
                invoice.ExtractionMethod = result.ExtractionMethod;
                invoice.OcrConfidence = result.Confidence;
                invoice.ExtractedAt = DateTime.UtcNow;
                invoice.Status = InvoiceStatus.Extracted;
                invoice.UpdatedAt = DateTime.UtcNow;

                // OcrText is already set on document by the extraction orchestrator
                document.UpdatedAt = DateTime.UtcNow;

                await _invoiceRepository.UpdateAsync(invoice, cancellationToken);
                await _documentRepository.UpdateAsync(document, cancellationToken);

                // Publish success event
                await _publishEndpoint.Publish(new InvoiceExtractedEvent
                {
                    InvoiceId = msg.InvoiceId,
                    TenantId = msg.TenantId,
                    ExtractionMethod = result.ExtractionMethod.ToString(),
                    Confidence = result.Confidence
                }, cancellationToken);

                // Publish completion progress
                await _publishEndpoint.Publish(new ExtractionProgressEvent
                {
                    InvoiceId = msg.InvoiceId,
                    TenantId = msg.TenantId,
                    Stage = "Complete",
                    Progress = 100,
                    Message = $"Extraction completed via {result.ExtractionMethod} with {result.Confidence:P1} confidence"
                }, cancellationToken);

                _logger.LogInformation(
                    "Extraction completed: InvoiceId={InvoiceId}, Method={Method}, Confidence={Confidence:P1}",
                    msg.InvoiceId, result.ExtractionMethod, result.Confidence);
            }
            else
            {
                await HandleFailureAsync(context, msg, invoice, result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Extraction threw exception for InvoiceId={InvoiceId}: {ErrorMessage}",
                msg.InvoiceId, ex.Message);

            await HandleFailureAsync(context, msg, invoice, ex.Message);
        }
    }

    private async Task HandleFailureAsync(
        ConsumeContext<ExtractInvoiceCommand> context,
        ExtractInvoiceCommand msg,
        Invoice invoice,
        string errorMessage)
    {
        var cancellationToken = context.CancellationToken;

        if (msg.RetryCount < MaxRetryCount)
        {
            var delay = TimeSpan.FromMinutes(Math.Pow(2, msg.RetryCount));

            _logger.LogWarning(
                "Extraction failed for InvoiceId={InvoiceId}, retrying in {DelayMinutes}m (attempt {RetryCount}/{MaxRetries}): {Error}",
                msg.InvoiceId, delay.TotalMinutes, msg.RetryCount + 1, MaxRetryCount, errorMessage);

            await context.ScheduleSend(
                new Uri("queue:extract-invoice"),
                delay,
                context.Message with { RetryCount = msg.RetryCount + 1 },
                cancellationToken);

            return;
        }

        // Max retries exceeded — mark invoice as failed
        _logger.LogError(
            "Max retries exceeded for InvoiceId={InvoiceId}. Marking as Failed.",
            msg.InvoiceId);

        invoice.Status = InvoiceStatus.Failed;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _invoiceRepository.UpdateAsync(invoice, cancellationToken);

        await _publishEndpoint.Publish(new InvoiceFailedEvent
        {
            InvoiceId = msg.InvoiceId,
            TenantId = msg.TenantId,
            FailureReason = errorMessage,
            StackTrace = errorMessage
        }, cancellationToken);

        await _publishEndpoint.Publish(new ExtractionProgressEvent
        {
            InvoiceId = msg.InvoiceId,
            TenantId = msg.TenantId,
            Stage = "Failed",
            Progress = 0,
            Message = $"Extraction failed after {MaxRetryCount} retries: {errorMessage}"
        }, cancellationToken);
    }
}
