using FluentAssertions;
using InvoiceFlow.Application.Consumers;
using InvoiceFlow.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace InvoiceFlow.UnitTests.Messaging;

public class ConsumerTests
{
    private readonly ILogger<InvoiceReceivedConsumer> _receivedLogger = Substitute.For<ILogger<InvoiceReceivedConsumer>>();
    private readonly ILogger<InvoiceExtractedConsumer> _extractedLogger = Substitute.For<ILogger<InvoiceExtractedConsumer>>();
    private readonly ILogger<InvoiceApprovedConsumer> _approvedLogger = Substitute.For<ILogger<InvoiceApprovedConsumer>>();
    private readonly ILogger<InvoiceRejectedConsumer> _rejectedLogger = Substitute.For<ILogger<InvoiceRejectedConsumer>>();
    private readonly ILogger<InvoiceCompliantConsumer> _compliantLogger = Substitute.For<ILogger<InvoiceCompliantConsumer>>();
    private readonly ILogger<InvoiceTransmittedConsumer> _transmittedLogger = Substitute.For<ILogger<InvoiceTransmittedConsumer>>();
    private readonly ILogger<InvoiceFailedConsumer> _failedLogger = Substitute.For<ILogger<InvoiceFailedConsumer>>();

    private static ConsumeContext<T> CreateContext<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        return context;
    }

    // ─── InvoiceReceivedConsumer ───────────────────────────────────────

    [Fact]
    public async Task InvoiceReceivedConsumer_ShouldLogInformation()
    {
        var consumer = new InvoiceReceivedConsumer(_receivedLogger);
        var invoiceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var message = new InvoiceReceivedEvent
        {
            InvoiceId = invoiceId,
            TenantId = tenantId,
            Source = "Email",
            FileName = "invoice_001.pdf"
        };
        var context = CreateContext(message);

        await consumer.Consume(context);

        _receivedLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(invoiceId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvoiceReceivedConsumer_ShouldNotLogErrors()
    {
        var consumer = new InvoiceReceivedConsumer(_receivedLogger);
        var context = CreateContext(new InvoiceReceivedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Source = "Upload",
            FileName = "test.pdf"
        });

        await consumer.Consume(context);

        _receivedLogger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceExtractedConsumer ──────────────────────────────────────

    [Fact]
    public async Task InvoiceExtractedConsumer_ShouldLogInformation()
    {
        var consumer = new InvoiceExtractedConsumer(_extractedLogger);
        var context = CreateContext(new InvoiceExtractedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ExtractionMethod = "OCR",
            Confidence = 0.95
        });

        await consumer.Consume(context);

        _extractedLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceApprovedConsumer ───────────────────────────────────────

    [Fact]
    public async Task InvoiceApprovedConsumer_ShouldLogInformation()
    {
        var consumer = new InvoiceApprovedConsumer(_approvedLogger);
        var context = CreateContext(new InvoiceApprovedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApprovedByUserId = Guid.NewGuid(),
            Comments = "Looks good"
        });

        await consumer.Consume(context);

        _approvedLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceRejectedConsumer ───────────────────────────────────────

    [Fact]
    public async Task InvoiceRejectedConsumer_ShouldLogWarning()
    {
        var consumer = new InvoiceRejectedConsumer(_rejectedLogger);
        var context = CreateContext(new InvoiceRejectedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RejectedByUserId = Guid.NewGuid(),
            Reason = "Invalid total amount"
        });

        await consumer.Consume(context);

        _rejectedLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceCompliantConsumer ──────────────────────────────────────

    [Fact]
    public async Task InvoiceCompliantConsumer_ShouldLogInformation()
    {
        var consumer = new InvoiceCompliantConsumer(_compliantLogger);
        var context = CreateContext(new InvoiceCompliantEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ComplianceModel = "Peppol",
            ComplianceId = "COMP-123"
        });

        await consumer.Consume(context);

        _compliantLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceTransmittedConsumer ────────────────────────────────────

    [Fact]
    public async Task InvoiceTransmittedConsumer_ShouldLogInformation()
    {
        var consumer = new InvoiceTransmittedConsumer(_transmittedLogger);
        var context = CreateContext(new InvoiceTransmittedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TransmissionId = "TX-999"
        });

        await consumer.Consume(context);

        _transmittedLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── InvoiceFailedConsumer ─────────────────────────────────────────

    [Fact]
    public async Task InvoiceFailedConsumer_ShouldLogError()
    {
        var consumer = new InvoiceFailedConsumer(_failedLogger);
        var context = CreateContext(new InvoiceFailedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FailureReason = "OCR engine timeout",
            StackTrace = "at InvoiceProcessor.Process()"
        });

        await consumer.Consume(context);

        _failedLogger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvoiceFailedConsumer_ShouldHandleNullStackTrace()
    {
        var consumer = new InvoiceFailedConsumer(_failedLogger);
        var context = CreateContext(new InvoiceFailedEvent
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FailureReason = "Unknown error",
            StackTrace = null
        });

        // Should not throw even with null StackTrace
        await consumer.Consume(context);

        _failedLogger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── All consumers implement IConsumer<T> ──────────────────────────

    [Theory]
    [InlineData(typeof(InvoiceReceivedConsumer), typeof(InvoiceReceivedEvent))]
    [InlineData(typeof(InvoiceExtractedConsumer), typeof(InvoiceExtractedEvent))]
    [InlineData(typeof(InvoiceApprovedConsumer), typeof(InvoiceApprovedEvent))]
    [InlineData(typeof(InvoiceRejectedConsumer), typeof(InvoiceRejectedEvent))]
    [InlineData(typeof(InvoiceCompliantConsumer), typeof(InvoiceCompliantEvent))]
    [InlineData(typeof(InvoiceTransmittedConsumer), typeof(InvoiceTransmittedEvent))]
    [InlineData(typeof(InvoiceFailedConsumer), typeof(InvoiceFailedEvent))]
    public void AllConsumers_ShouldImplementIConsumer(Type consumerType, Type eventType)
    {
        var genericType = typeof(IConsumer<>).MakeGenericType(eventType);
        genericType.IsAssignableFrom(consumerType).Should().BeTrue(
            $"{consumerType.Name} should implement IConsumer<{eventType.Name}>");
    }

    // ─── Consumer instantiation (all should have ILogger constructor) ──

    [Fact]
    public void AllConsumerTypes_ShouldBeCreatableWithLogger()
    {
        var consumerTypes = new[]
        {
            typeof(InvoiceReceivedConsumer),
            typeof(InvoiceExtractedConsumer),
            typeof(InvoiceApprovedConsumer),
            typeof(InvoiceRejectedConsumer),
            typeof(InvoiceCompliantConsumer),
            typeof(InvoiceTransmittedConsumer),
            typeof(InvoiceFailedConsumer)
        };

        foreach (var type in consumerTypes)
        {
            var loggerType = typeof(ILogger<>).MakeGenericType(type);
            var ctor = type.GetConstructor(new[] { loggerType });
            ctor.Should().NotBeNull($"{type.Name} should have a constructor with ILogger<{type.Name}>");
        }
    }
}
