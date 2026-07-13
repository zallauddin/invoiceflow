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
/// Tests for FtpSftpIngestionConsumer — verifies delegation to IFtpSftpIngestionService
/// and exception handling behavior.
/// </summary>
public class FtpSftpIngestionConsumerTests
{
    private readonly IFtpSftpIngestionService _mockIngestionService = Substitute.For<IFtpSftpIngestionService>();
    private readonly ILogger<FtpSftpIngestionConsumer> _mockLogger = Substitute.For<ILogger<FtpSftpIngestionConsumer>>();

    private FtpSftpIngestionConsumer CreateSut()
        => new(_mockIngestionService, _mockLogger);

    private static TriggerFtpSftpIngestionCommand CreateCommand(
        Guid? tenantId = null,
        Guid? correlationId = null)
    {
        return new TriggerFtpSftpIngestionCommand
        {
            TenantId = tenantId ?? Guid.NewGuid(),
            CorrelationId = correlationId ?? Guid.NewGuid()
        };
    }

    private static ConsumeContext<TriggerFtpSftpIngestionCommand> CreateContext(
        TriggerFtpSftpIngestionCommand command,
        CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<ConsumeContext<TriggerFtpSftpIngestionCommand>>();
        context.Message.Returns(command);
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }

    // ─── Successful Consumption ──────────────────────────────────────

    [Fact]
    public async Task Consume_SuccessfulPoll_CallsPollAsync()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
            {
                Success = true,
                ProcessedCount = 2,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockIngestionService.Received(1).PollAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulPoll_LogsInformationWithCounts()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
            {
                Success = true,
                ProcessedCount = 5,
                FailedCount = 1,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("5") && o.ToString()!.Contains("1")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task Consume_SuccessfulPoll_DoesNotThrow()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
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
    public async Task Consume_PassesCancellationTokenToService()
    {
        var command = CreateCommand();
        using var cts = new CancellationTokenSource();
        var context = CreateContext(command, cts.Token);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
            {
                Success = true,
                ProcessedCount = 0,
                FailedCount = 0,
                CreatedDocuments = []
            });

        var sut = CreateSut();
        await sut.Consume(context);

        await _mockIngestionService.Received(1).PollAsync(cts.Token);
    }

    // ─── Exception Handling ───────────────────────────────────────────

    [Fact]
    public async Task Consume_ServiceThrowsException_LogsError()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Throws(new IOException("SFTP connection refused"));

        var sut = CreateSut();
        var act = async () => await sut.Consume(context);

        await act.Should().ThrowAsync<IOException>();

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

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Throws(new IOException("SFTP connection failed"));

        var sut = CreateSut();
        var act = async () => await sut.Consume(context);

        await act.Should().ThrowAsync<IOException>()
            .WithMessage("SFTP connection failed");
    }

    [Fact]
    public async Task Consume_FailedPoll_LogsWarningWithError()
    {
        var command = CreateCommand();
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
            {
                Success = false,
                ProcessedCount = 0,
                FailedCount = 3,
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

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
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

    [Fact]
    public async Task Consume_IncludesTenantIdInLogMessage()
    {
        var tenantId = Guid.NewGuid();
        var command = CreateCommand(tenantId: tenantId);
        var context = CreateContext(command);

        _mockIngestionService.PollAsync(Arg.Any<CancellationToken>())
            .Returns(new FtpSftpIngestionResult
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
            Arg.Is<object>(o => o.ToString()!.Contains(tenantId.ToString())),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
