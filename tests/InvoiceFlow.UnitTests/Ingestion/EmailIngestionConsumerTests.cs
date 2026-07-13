using FluentAssertions;
using InvoiceFlow.Application.Consumers;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Core.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.Ingestion;

/// <summary>
/// Tests for EmailIngestionConsumer — verifies delegation to IEmailIngestionService
/// and exception handling behavior.
/// </summary>
public class EmailIngestionConsumerTests
{
    private readonly IEmailIngestionService _mockIngestionService = Substitute.For<IEmailIngestionService>();
    private readonly ILogger<EmailIngestionConsumer> _mockLogger = Substitute.For<ILogger<EmailIngestionConsumer>>();

    private EmailIngestionConsumer CreateSut()
        => new(_mockIngestionService, _mockLogger);

    private static TriggerEmailIngestionCommand CreateCommand(
        Guid? tenantId = null,
        Guid? correlationId = null)
    {
        return new TriggerEmailIngestionCommand
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
    }

    private static ConsumeContext<TriggerEmailIngestionCommand> CreateContext(
        TriggerEmailIngestionCommand command,
        CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<ConsumeContext<TriggerEmailIngestionCommand>>();
        context.Message.Returns(command);
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }

    // ─── Successful Consumption ──────────────────────────────────────

    [Fact]
    public async Task Consume_SuccessfulPoll_CallsPollEmailsAsync()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 2,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockIngestionService.Received(1).PollEmailsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulPoll_LogsInformationWithCounts()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 3,
                FailedCount = 1,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("3") && o.ToString()!.Contains("1")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Consume_SuccessfulPoll_DoesNotThrow()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 0,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        var act = async () => await sut.Consume(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_PasssesCancellationTokenToService()
    {
        var command = CreateCommand();
        using var cts = new CancellationTokenSource();
        var context = CreateContext(command, cts.Token);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 0,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockIngestionService.Received(1).PollEmailsAsync(cts.Token);
    }

    // ─── Exception Handling ───────────────────────────────────────────

    [Fact]
    public async Task Consume_ServiceThrowsException_LogsError()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("IMAP connection failed"));

        var sut = CreateSut();
        var act = async () => await sut.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>();

        _mockLogger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("failed with exception")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Consume_ServiceThrowsException_ReThrowsToMassTransit()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Connection timeout"));

        var sut = CreateSut();
        var act = async () => await sut.Consume(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection timeout");
    }

    [Fact]
    public async Task Consume_FailedPoll_LogsWarningWithError()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = 2,
                ErrorMessage = "Authentication failed"
            });

        var sut = CreateSut();
        await sut.Consume(context);

        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("errors")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Consume_IncludesCorrelationIdInLogMessage()
    {
        var correlationId = Guid.NewGuid();
        var command = CreateCommand(correlationId: correlationId);
        var context = CreateContext(command);

        _mockIngestionService.PollEmailsAsync(Arg.Any<CancellationToken>())
            .Returns(new EmailIngestionResult
            {
                Success = true,
                ProcessedCount = 0,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains(correlationId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
