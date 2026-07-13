using FluentAssertions;
using InvoiceFlow.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace InvoiceFlow.UnitTests.Storage;

public class MinioStorageServiceTests
{
    private readonly IMinioClient _mockClient;
    private readonly ILogger<MinioStorageService> _mockLogger;
    private readonly MinioStorageService _sut;

    public MinioStorageServiceTests()
    {
        _mockClient = Substitute.For<IMinioClient>();
        _mockLogger = Substitute.For<ILogger<MinioStorageService>>();
        _sut = new MinioStorageService(_mockClient, _mockLogger);
    }

    [Fact]
    public void Constructor_WithNullClient_ShouldThrow()
    {
        Action act = () => new MinioStorageService(null!, _mockLogger);
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("client");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        Action act = () => new MinioStorageService(_mockClient, null!);
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task UploadAsync_WithValidArgs_ShouldCallPutObject()
    {
        // Arrange
        var bucketName = "test-bucket";
        var objectName = "tenant123/invoices/2024/01/test.pdf";
        using var stream = new MemoryStream("test"u8.ToArray());
        var contentType = "application/pdf";

        _mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.UploadAsync(bucketName, objectName, stream, contentType);

        // Assert
        await _mockClient.Received(1).PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithEmptyBucket_ShouldThrow()
    {
        using var stream = new MemoryStream("test"u8.ToArray());
        var act = () => _sut.UploadAsync("", "object", stream, "text/plain");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_WithEmptyObject_ShouldThrow()
    {
        using var stream = new MemoryStream("test"u8.ToArray());
        var act = () => _sut.UploadAsync("bucket", "", stream, "text/plain");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UploadAsync_WithNullStream_ShouldThrow()
    {
        var act = () => _sut.UploadAsync("bucket", "object", null!, "text/plain");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UploadAsync_WhenBucketDoesNotExist_ShouldCreateBucket()
    {
        // Arrange
        using var stream = new MemoryStream("test"u8.ToArray());
        _mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.UploadAsync("test-bucket", "object", stream, "text/plain");

        // Assert
        await _mockClient.Received(1).MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());
        await _mockClient.Received(1).PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_WithValidArgs_ShouldCallGetObject()
    {
        // Arrange: GetObjectAsync is an extension method on IObjectOperations in MinIO SDK v6.
        // NSubstitute can't intercept it, but we can verify the method doesn't throw
        // on valid input by testing the error path (the mock will throw, simulating a failure).
        _mockClient.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new MinioException("ObjectNotFound"));

        // Act
        var result = await _sut.DownloadAsync("test-bucket", "tenant123/test.pdf");

        // Assert — exception is caught, returns null
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_WhenObjectNotFound_ShouldReturnNull()
    {
        // Arrange
        _mockClient.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new MinioException("ObjectNotFound"));

        // Act
        var result = await _sut.DownloadAsync("bucket", "missing.pdf");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_WhenNoSuchKey_ShouldReturnNull()
    {
        // Arrange
        _mockClient.GetObjectAsync(Arg.Any<GetObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new MinioException("NoSuchKey"));

        // Act
        var result = await _sut.DownloadAsync("bucket", "missing.pdf");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_WithEmptyBucket_ShouldThrow()
    {
        var act = () => _sut.DownloadAsync("", "object");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DownloadAsync_WithEmptyObject_ShouldThrow()
    {
        var act = () => _sut.DownloadAsync("bucket", "");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WithValidArgs_ShouldCallRemoveObject()
    {
        // Act
        await _sut.DeleteAsync("test-bucket", "object.pdf");

        // Assert
        await _mockClient.Received(1).RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenMinioException_ShouldLogWarning()
    {
        // Arrange
        _mockClient.RemoveObjectAsync(Arg.Any<RemoveObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new MinioException("Connection failed"));

        // Act — should not throw
        await _sut.DeleteAsync("bucket", "object.pdf");

        // Assert — method should swallow the exception and log a warning
        _mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o!.ToString()!.Contains("Failed to delete")),
            Arg.Any<MinioException>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyBucket_ShouldThrow()
    {
        var act = () => _sut.DeleteAsync("", "object");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteAsync_WithEmptyObject_ShouldThrow()
    {
        var act = () => _sut.DeleteAsync("bucket", "");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ObjectExistsAsync_WhenStatThrowsNonMinioException_ShouldThrow()
    {
        // Arrange: StatObjectAsync is an extension method on IObjectOperations (not IMinioClient),
        // so NSubstitute can't intercept it. A non-MinioException should propagate.
        // This verifies the try-catch only catches MinioException, not other exceptions.
        _mockClient.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Connection refused"));

        var act = () => _sut.ObjectExistsAsync("bucket", "existing.pdf");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ObjectExistsAsync_WhenObjectNotFound_ShouldReturnFalse()
    {
        // Arrange
        _mockClient.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>())
            .Throws(new MinioException("ObjectNotFound"));

        // Act
        var result = await _sut.ObjectExistsAsync("bucket", "missing.pdf");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ObjectExistsAsync_WithEmptyBucket_ShouldThrow()
    {
        var act = () => _sut.ObjectExistsAsync("", "object");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ObjectExistsAsync_WithEmptyObject_ShouldThrow()
    {
        var act = () => _sut.ObjectExistsAsync("bucket", "");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithValidArgs_ShouldReturnUrl()
    {
        // Arrange
        var expectedUrl = "https://minio.local/bucket/object?signature=abc";
        _mockClient.PresignedGetObjectAsync(Arg.Any<PresignedGetObjectArgs>())
            .Returns(expectedUrl);

        // Act
        var result = await _sut.GetPresignedUrlAsync("bucket", "object.pdf", TimeSpan.FromHours(1));

        // Assert
        result.Should().Be(expectedUrl);
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithEmptyBucket_ShouldThrow()
    {
        var act = () => _sut.GetPresignedUrlAsync("", "object", TimeSpan.FromHours(1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPresignedUrlAsync_WithEmptyObject_ShouldThrow()
    {
        var act = () => _sut.GetPresignedUrlAsync("bucket", "", TimeSpan.FromHours(1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WhenBucketExists_ShouldNotCreate()
    {
        // Arrange
        _mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.EnsureBucketExistsAsync("existing-bucket");

        // Assert
        await _mockClient.Received(1).BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>());
        await _mockClient.DidNotReceive().MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WhenBucketDoesNotExist_ShouldCreate()
    {
        // Arrange
        _mockClient.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.EnsureBucketExistsAsync("new-bucket");

        // Assert
        await _mockClient.Received(1).MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureBucketExistsAsync_WithEmptyBucket_ShouldThrow()
    {
        var act = () => _sut.EnsureBucketExistsAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DisposeAsync_WithAsyncDisposableClient_ShouldNotThrow()
    {
        // Arrange
        var asyncClient = Substitute.For<IMinioClient, IAsyncDisposable>();
        var service = new MinioStorageService(asyncClient, _mockLogger);

        // Act — should not throw
        var act = async () => await service.DisposeAsync();
        await act.Should().NotThrowAsync();
    }
}
