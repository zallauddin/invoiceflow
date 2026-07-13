using FluentAssertions;
using InvoiceFlow.Application.Consumers;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.AI.Extraction;

/// <summary>
/// Tests for ExtractInvoiceConsumer — extraction orchestration, retry logic,
/// progress events, and failure handling.
/// </summary>
public class ExtractInvoiceConsumerTests
{
    private readonly IExtractionOrchestrator _mockOrchestrator = Substitute.For<IExtractionOrchestrator>();
    private readonly IRepository<Invoice> _mockInvoiceRepo = Substitute.For<IRepository<Invoice>>();
    private readonly IRepository<Document> _mockDocumentRepo = Substitute.For<IRepository<Document>>();
    private readonly IPublishEndpoint _mockPublishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly ILogger<ExtractInvoiceConsumer> _mockLogger = Substitute.For<ILogger<ExtractInvoiceConsumer>>();

    private ExtractInvoiceConsumer CreateSut()
        => new(_mockOrchestrator, _mockInvoiceRepo, _mockDocumentRepo, _mockPublishEndpoint, _mockLogger);

    private static ExtractInvoiceCommand CreateCommand(
        Guid? invoiceId = null,
        Guid? documentId = null,
        Guid? tenantId = null,
        int retryCount = 0)
    {
        return new ExtractInvoiceCommand
        {
            InvoiceId = invoiceId ?? Guid.NewGuid(),
            DocumentId = documentId ?? Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            StoragePath = "/storage/invoice.pdf",
            MimeType = "application/pdf",
            FileName = "invoice.pdf",
            RetryCount = retryCount
        };
    }

    private static ConsumeContext<ExtractInvoiceCommand> CreateContext(
        ExtractInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<ConsumeContext<ExtractInvoiceCommand>>();
        context.Message.Returns(command);
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }

    // ─── Successful Extraction Flow ───────────────────────────────────

    [Fact]
    public async Task Consume_SuccessfulExtraction_PublishesExtractedEvent()
    {
        var invoiceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var command = CreateCommand(invoiceId, documentId, tenantId);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = invoiceId, TenantId = tenantId, Status = InvoiceStatus.Received };
        var document = new Document { Id = documentId, TenantId = tenantId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(documentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Ocr,
                Confidence = 0.92,
                Invoice = invoice,
                DomainEvents = new List<IDomainEvent>
                {
                    new InvoiceExtractedEvent
                    {
                        InvoiceId = invoiceId,
                        TenantId = tenantId,
                        ExtractionMethod = "Ocr",
                        Confidence = 0.92
                    }
                }
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<InvoiceExtractedEvent>(e =>
                e.InvoiceId == invoiceId &&
                e.TenantId == tenantId &&
                e.ExtractionMethod == "Ocr"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulExtraction_UpdatesInvoiceRepository()
    {
        var invoiceId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var command = CreateCommand(invoiceId, documentId);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = invoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = documentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(documentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Llm,
                Confidence = 0.88,
                Invoice = invoice
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockInvoiceRepo.Received(1).UpdateAsync(invoice, Arg.Any<CancellationToken>());
        invoice.Status.Should().Be(InvoiceStatus.Extracted);
        invoice.ExtractionMethod.Should().Be(ExtractionMethod.Llm);
    }

    [Fact]
    public async Task Consume_SuccessfulExtraction_UpdatesDocumentRepository()
    {
        var invoiceId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var command = CreateCommand(invoiceId, documentId);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = invoiceId };
        var document = new Document { Id = documentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(documentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Ocr,
                Confidence = 0.90,
                Invoice = invoice
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockDocumentRepo.Received(1).UpdateAsync(document, Arg.Any<CancellationToken>());
    }

    // ─── Progress Events ──────────────────────────────────────────────

    [Fact]
    public async Task Consume_PublishesProgressEventAtStart()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Ocr,
                Confidence = 0.90,
                Invoice = invoice
            });

        var sut = CreateSut();
        await sut.Consume(context);

        // Should publish initial progress (OCR stage)
        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<ExtractionProgressEvent>(e =>
                e.InvoiceId == command.InvoiceId &&
                e.Stage == "OCR" &&
                e.Progress == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PublishesCompletionProgressOnSuccess()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Ocr,
                Confidence = 0.90,
                Invoice = invoice
            });

        var sut = CreateSut();
        await sut.Consume(context);

        // Should publish completion progress
        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<ExtractionProgressEvent>(e =>
                e.InvoiceId == command.InvoiceId &&
                e.Stage == "Complete" &&
                e.Progress == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PublishesFailedProgressOnMaxRetries()
    {
        var command = CreateCommand(retryCount: 3); // Max retries exceeded
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("Extraction failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should publish failed progress
        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<ExtractionProgressEvent>(e =>
                e.InvoiceId == command.InvoiceId &&
                e.Stage == "Failed" &&
                e.Progress == 0),
            Arg.Any<CancellationToken>());
    }

    // ─── Retry Logic with Exponential Backoff ─────────────────────────

    [Fact]
    public async Task Consume_FirstFailure_SchedulesRetryWithExponentialDelay()
    {
        var command = CreateCommand(retryCount: 0);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("OCR failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should schedule retry (retryCount 0 -> delay = 2^0 = 1 minute)
        await context.Received(1).ScheduleSend(
            Arg.Is(new Uri("queue:extract-invoice")),
            Arg.Is(TimeSpan.FromMinutes(1)),
            Arg.Is<ExtractInvoiceCommand>(c => c.RetryCount == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SecondFailure_SchedulesRetryWithLongerDelay()
    {
        var command = CreateCommand(retryCount: 1);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("LLM failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should schedule retry (retryCount 1 -> delay = 2^1 = 2 minutes)
        await context.Received(1).ScheduleSend(
            Arg.Is(new Uri("queue:extract-invoice")),
            Arg.Is(TimeSpan.FromMinutes(2)),
            Arg.Is<ExtractInvoiceCommand>(c => c.RetryCount == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ThirdFailure_SchedulesRetryWithFourMinuteDelay()
    {
        var command = CreateCommand(retryCount: 2);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("Template failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should schedule retry (retryCount 2 -> delay = 2^2 = 4 minutes)
        await context.Received(1).ScheduleSend(
            Arg.Is(new Uri("queue:extract-invoice")),
            Arg.Is(TimeSpan.FromMinutes(4)),
            Arg.Is<ExtractInvoiceCommand>(c => c.RetryCount == 3),
            Arg.Any<CancellationToken>());
    }

    // ─── Max Retries Publishes InvoiceFailedEvent ─────────────────────

    [Fact]
    public async Task Consume_MaxRetriesExceeded_PublishesInvoiceFailedEvent()
    {
        var invoiceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = CreateCommand(invoiceId: invoiceId, tenantId: tenantId, retryCount: 3);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = invoiceId, TenantId = tenantId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("All stages failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<InvoiceFailedEvent>(e =>
                e.InvoiceId == invoiceId &&
                e.TenantId == tenantId &&
                e.FailureReason == "All stages failed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_MaxRetriesExceeded_MarksInvoiceAsFailed()
    {
        var invoiceId = Guid.NewGuid();
        var command = CreateCommand(invoiceId: invoiceId, retryCount: 3);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = invoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(invoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("Failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        invoice.Status.Should().Be(InvoiceStatus.Failed);
        await _mockInvoiceRepo.Received(1).UpdateAsync(invoice, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_MaxRetriesExceeded_DoesNotScheduleAnotherRetry()
    {
        var command = CreateCommand(retryCount: 3);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("Failed"));

        var sut = CreateSut();
        await sut.Consume(context);

        await context.DidNotReceive().ScheduleSend(
            Arg.Any<Uri>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<ExtractInvoiceCommand>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Entity Not Found Handling ────────────────────────────────────

    [Fact]
    public async Task Consume_InvoiceNotFound_DoesNotCallOrchestrator()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockOrchestrator.DidNotReceive().ProcessAsync(
            Arg.Any<Document>(),
            Arg.Any<Invoice>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_InvoiceNotFound_DoesNotPublishEvents()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>())
            .Returns((Invoice?)null);

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockPublishEndpoint.DidNotReceive().Publish(
            Arg.Any<InvoiceExtractedEvent>(),
            Arg.Any<CancellationToken>());
        await _mockPublishEndpoint.DidNotReceive().Publish(
            Arg.Any<ExtractionProgressEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_DocumentNotFound_DoesNotCallOrchestrator()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId };
        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockOrchestrator.DidNotReceive().ProcessAsync(
            Arg.Any<Document>(),
            Arg.Any<Invoice>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Exception Handling ───────────────────────────────────────────

    [Fact]
    public async Task Consume_OrchestratorThrows_SchedulesRetry()
    {
        var command = CreateCommand(retryCount: 0);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Unexpected error"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should schedule retry on exception (when retryCount < max)
        await context.Received(1).ScheduleSend(
            Arg.Any<Uri>(),
            Arg.Any<TimeSpan>(),
            Arg.Is<ExtractInvoiceCommand>(c => c.RetryCount == 1),
            Arg.Any<CancellationToken>());
    }

    // ─── Cancellation Token ───────────────────────────────────────────

    [Fact]
    public async Task Consume_WithCancellation_PassesTokenToOrchestrator()
    {
        var command = CreateCommand();
        using var cts = new CancellationTokenSource();
        var context = CreateContext(command, cts.Token);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(new ExtractionOrchestratorResult
            {
                Success = true,
                ExtractionMethod = ExtractionMethod.Ocr,
                Confidence = 0.90,
                Invoice = invoice
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockOrchestrator.Received(1).ProcessAsync(
            document,
            invoice,
            Arg.Any<CancellationToken>());
    }

    // ─── Retry Does Not Happen on Max ─────────────────────────────────

    [Fact]
    public async Task Consume_RetryCountExactlyAtMax_PublishesFailure()
    {
        // MaxRetryCount = 3, so RetryCount 3 means we've exhausted retries
        var command = CreateCommand(retryCount: 3);
        var context = CreateContext(command);

        var invoice = new Invoice { Id = command.InvoiceId, Status = InvoiceStatus.Received };
        var document = new Document { Id = command.DocumentId, MimeType = "image/png" };

        _mockInvoiceRepo.GetByIdAsync(command.InvoiceId, Arg.Any<CancellationToken>()).Returns(invoice);
        _mockDocumentRepo.GetByIdAsync(command.DocumentId, Arg.Any<CancellationToken>()).Returns(document);
        _mockOrchestrator.ProcessAsync(document, invoice, Arg.Any<CancellationToken>())
            .Returns(ExtractionOrchestratorResult.Fail("Timeout"));

        var sut = CreateSut();
        await sut.Consume(context);

        // Should publish failure, not schedule retry
        await _mockPublishEndpoint.Received(1).Publish(
            Arg.Is<InvoiceFailedEvent>(e => e.FailureReason == "Timeout"),
            Arg.Any<CancellationToken>());
        await context.DidNotReceive().ScheduleSend(
            Arg.Any<Uri>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<ExtractInvoiceCommand>(),
            Arg.Any<CancellationToken>());
    }
}
