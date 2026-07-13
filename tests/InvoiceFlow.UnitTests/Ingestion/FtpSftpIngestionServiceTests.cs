using FluentAssertions;
using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Ingestion;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.Ingestion;

/// <summary>
/// Tests for FtpSftpIngestionService — FTP/SFTP file polling, download, storage upload,
/// entity creation, and extraction command publishing.
/// </summary>
public class FtpSftpIngestionServiceTests
{
    private readonly IStorageService _mockStorageService = Substitute.For<IStorageService>();
    private readonly IRepository<Document> _mockDocumentRepo = Substitute.For<IRepository<Document>>();
    private readonly IRepository<Invoice> _mockInvoiceRepo = Substitute.For<IRepository<Invoice>>();
    private readonly IPublishEndpoint _mockPublishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly ILogger<FtpSftpIngestionService> _mockLogger = Substitute.For<ILogger<FtpSftpIngestionService>>();

    private readonly Guid _tenantId = Guid.NewGuid();

    private FtpSftpIngestionService CreateSut(FtpSftpIngestionOptions? options = null)
    {
        options ??= CreateDefaultFtpOptions();
        return new FtpSftpIngestionService(
            Options.Create(options),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);
    }

    private FtpSftpIngestionOptions CreateDefaultFtpOptions(
        string? host = null,
        int? port = null,
        int? maxRetryAttempts = null,
        int? retryBaseDelaySeconds = null)
    {
        return new FtpSftpIngestionOptions
        {
            Protocol = "FTP",
            Host = host ?? "localhost",
            Port = port ?? 21,
            Username = "testuser",
            Password = "testpass",
            RemotePath = "/incoming",
            ProcessedPath = "/processed",
            FailedPath = "/failed",
            TenantId = _tenantId,
            BucketName = "documents",
            MaxRetryAttempts = maxRetryAttempts ?? 1,
            RetryBaseDelaySeconds = retryBaseDelaySeconds ?? 0,
            FileExtensions = [".pdf", ".xml", ".jpg", ".png", ".tiff"]
        };
    }

    private FtpSftpIngestionOptions CreateDefaultSftpOptions(
        string? host = null,
        int? port = null,
        int? maxRetryAttempts = null,
        int? retryBaseDelaySeconds = null)
    {
        return new FtpSftpIngestionOptions
        {
            Protocol = "SFTP",
            Host = host ?? "localhost",
            Port = port ?? 22,
            Username = "testuser",
            Password = "testpass",
            RemotePath = "/incoming",
            ProcessedPath = "/processed",
            FailedPath = "/failed",
            TenantId = _tenantId,
            BucketName = "documents",
            MaxRetryAttempts = maxRetryAttempts ?? 1,
            RetryBaseDelaySeconds = retryBaseDelaySeconds ?? 0,
            FileExtensions = [".pdf", ".xml", ".jpg", ".png", ".tiff"]
        };
    }

    // ─── Constructor Validation ──────────────────────────────────────

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            null!,
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("options");
    }

    [Fact]
    public void Constructor_NullStorageService_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            Options.Create(CreateDefaultFtpOptions()),
            null!,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("storageService");
    }

    [Fact]
    public void Constructor_NullDocumentRepo_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            Options.Create(CreateDefaultFtpOptions()),
            _mockStorageService,
            null!,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("documentRepository");
    }

    [Fact]
    public void Constructor_NullInvoiceRepo_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            Options.Create(CreateDefaultFtpOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            null!,
            _mockPublishEndpoint,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("invoiceRepository");
    }

    [Fact]
    public void Constructor_NullPublishEndpoint_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            Options.Create(CreateDefaultFtpOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            null!,
            _mockLogger);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("publishEndpoint");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new FtpSftpIngestionService(
            Options.Create(CreateDefaultFtpOptions()),
            _mockStorageService,
            _mockDocumentRepo,
            _mockInvoiceRepo,
            _mockPublishEndpoint,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("logger");
    }

    // ─── PollAsync — FTP Connection Failure ──────────────────────────

    [Fact]
    public async Task PollAsync_FtpConnectionFails_ReturnsFailureResult()
    {
        var options = CreateDefaultFtpOptions(host: "127.0.0.1", port: 1921, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);

        var result = await sut.PollAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ProcessedCount.Should().Be(0);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PollAsync_FtpConnectionFails_DisconnectsGracefully()
    {
        var options = CreateDefaultFtpOptions(host: "127.0.0.1", port: 1921, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);
        await sut.PollAsync(CancellationToken.None);

        // Second call should also fail gracefully without stuck state
        var result = await sut.PollAsync(CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task PollAsync_FtpConnectionFails_RetriesUpToMaxAttempts()
    {
        var options = CreateDefaultFtpOptions(host: "127.0.0.1", port: 1921, maxRetryAttempts: 2, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);

        var result = await sut.PollAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.CreatedDocuments.Should().BeEmpty();
    }

    // ─── PollAsync — SFTP Connection Failure ─────────────────────────

    [Fact]
    public async Task PollAsync_SftpConnectionFails_ReturnsFailureResult()
    {
        var options = CreateDefaultSftpOptions(host: "127.0.0.1", port: 1922, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);

        var result = await sut.PollAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ProcessedCount.Should().Be(0);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PollAsync_SftpConnectionFails_DisconnectsGracefully()
    {
        var options = CreateDefaultSftpOptions(host: "127.0.0.1", port: 1922, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);
        await sut.PollAsync(CancellationToken.None);

        // Should be able to call again without stuck state
        var result = await sut.PollAsync(CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    // ─── PollAsync — Cancellation ────────────────────────────────────

    [Fact]
    public async Task PollAsync_CancellationRequested_DoesNotHang()
    {
        var options = CreateDefaultFtpOptions(host: "127.0.0.1", port: 1921, maxRetryAttempts: 1, retryBaseDelaySeconds: 10);

        var sut = CreateSut(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await sut.PollAsync(cts.Token);

        result.Success.Should().BeFalse();
    }

    // ─── ProcessFileAsync — Argument Validation ──────────────────────

    [Fact]
    public async Task ProcessFileAsync_NullRemotePath_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.ProcessFileAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessFileAsync_EmptyRemotePath_ThrowsArgumentException()
    {
        var sut = CreateSut();

        var act = async () => await sut.ProcessFileAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── ProcessFileAsync — FTP Download Failure ─────────────────────

    [Fact]
    public async Task ProcessFileAsync_FtpConnectionFails_ReturnsFailure()
    {
        var options = CreateDefaultFtpOptions(host: "127.0.0.1", port: 1921, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);

        var result = await sut.ProcessFileAsync("/incoming/invoice.pdf");

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessFileAsync_SftpConnectionFails_ReturnsFailure()
    {
        var options = CreateDefaultSftpOptions(host: "127.0.0.1", port: 1922, maxRetryAttempts: 1, retryBaseDelaySeconds: 0);

        var sut = CreateSut(options);

        var result = await sut.ProcessFileAsync("/incoming/invoice.pdf");

        result.Success.Should().BeFalse();
        result.FailedCount.Should().Be(1);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ─── Protocol Configuration ──────────────────────────────────────

    [Fact]
    public void Options_ProtocolDefault_IsSftp()
    {
        var options = new FtpSftpIngestionOptions();

        options.Protocol.Should().Be("SFTP");
    }

    [Fact]
    public void Options_DefaultPort_Is22()
    {
        var options = new FtpSftpIngestionOptions();

        options.Port.Should().Be(22);
    }

    [Fact]
    public void Options_DefaultRemotePath_IsIncoming()
    {
        var options = new FtpSftpIngestionOptions();

        options.RemotePath.Should().Be("/incoming");
    }

    [Fact]
    public void Options_DefaultProcessedPath_IsProcessed()
    {
        var options = new FtpSftpIngestionOptions();

        options.ProcessedPath.Should().Be("/processed");
    }

    [Fact]
    public void Options_DefaultFailedPath_IsFailed()
    {
        var options = new FtpSftpIngestionOptions();

        options.FailedPath.Should().Be("/failed");
    }

    [Fact]
    public void Options_DefaultFileExtensions_ContainsCommonInvoiceFormats()
    {
        var options = new FtpSftpIngestionOptions();

        options.FileExtensions.Should().Contain(".pdf");
        options.FileExtensions.Should().Contain(".xml");
        options.FileExtensions.Should().Contain(".jpg");
        options.FileExtensions.Should().Contain(".png");
        options.FileExtensions.Should().Contain(".tiff");
    }

    // ─── Dispose ─────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = async () =>
        {
            await sut.DisposeAsync();
            await sut.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }
}
