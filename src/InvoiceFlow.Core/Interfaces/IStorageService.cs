namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Object storage abstraction for file upload/download/delete.
/// Implementations handle tenant-aware path conventions and bucket management.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Upload a file to object storage.
    /// </summary>
    /// <param name="bucketName">Target bucket (e.g. "documents", "invoices").</param>
    /// <param name="objectName">Object path using convention: {tenantId}/{category}/{yyyy/MM}/{filename}</param>
    /// <param name="stream">File content stream.</param>
    /// <param name="contentType">MIME type (e.g. "application/pdf").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UploadAsync(string bucketName, string objectName, Stream stream, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a file from object storage. Returns null if not found.
    /// </summary>
    Task<Stream?> DownloadAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a file from object storage.
    /// </summary>
    Task DeleteAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an object exists in storage.
    /// </summary>
    Task<bool> ObjectExistsAsync(string bucketName, string objectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a presigned URL for time-limited access (e.g. download links).
    /// </summary>
    /// <param name="expiry">URL validity duration.</param>
    Task<string> GetPresignedUrlAsync(string bucketName, string objectName, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure a bucket exists, creating it if necessary.
    /// </summary>
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken = default);
}
