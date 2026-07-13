using InvoiceFlow.Core.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.Storage;

/// <summary>
/// MinIO object storage implementation with tenant-aware path conventions.
/// Path convention: {tenantId}/{category}/{yyyy/MM}/{filename}
/// </summary>
public class MinioStorageService : IStorageService, IAsyncDisposable
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IMinioClient client, ILogger<MinioStorageService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task UploadAsync(string bucketName, string objectName, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);
        ArgumentNullException.ThrowIfNull(stream);

        await EnsureBucketExistsAsync(bucketName, cancellationToken);

        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, cancellationToken);

        _logger.LogInformation("Uploaded object {ObjectName} to bucket {BucketName} ({Size} bytes)",
            objectName, bucketName, stream.Length);
    }

    public async Task<Stream?> DownloadAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        try
        {
            var ms = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(ms, ct);
                    ms.Position = 0;
                });

            await _client.GetObjectAsync(args, cancellationToken);

            _logger.LogInformation("Downloaded object {ObjectName} from bucket {BucketName}", objectName, bucketName);
            return ms;
        }
        catch (MinioException ex) when (ex.Message.Contains("ObjectNotFound") || ex.Message.Contains("NoSuchKey"))
        {
            _logger.LogWarning("Object not found: {ObjectName} in bucket {BucketName}", objectName, bucketName);
            return null;
        }
    }

    public async Task DeleteAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _client.RemoveObjectAsync(args, cancellationToken);

            _logger.LogInformation("Deleted object {ObjectName} from bucket {BucketName}", objectName, bucketName);
        }
        catch (MinioException ex)
        {
            _logger.LogWarning(ex, "Failed to delete object {ObjectName} from bucket {BucketName}", objectName, bucketName);
        }
    }

    public async Task<bool> ObjectExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _client.StatObjectAsync(args, cancellationToken);
            return true;
        }
        catch (MinioException)
        {
            return false;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucketName, string objectName, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithExpiry((int)expiry.TotalSeconds);

        var url = await _client.PresignedGetObjectAsync(args);

        _logger.LogInformation("Generated presigned URL for {ObjectName} in bucket {BucketName} (expires {Expiry}s)",
            objectName, bucketName, (int)expiry.TotalSeconds);

        return url;
    }

    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);

        var exists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName), cancellationToken);

        if (!exists)
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName), cancellationToken);

            _logger.LogInformation("Created bucket {BucketName}", bucketName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }
}
