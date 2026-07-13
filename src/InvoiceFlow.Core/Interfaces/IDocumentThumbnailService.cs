using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Status of a thumbnail generation job.
/// </summary>
public enum ThumbnailJobStatus
{
    /// <summary>Job is queued and waiting to be processed.</summary>
    Pending = 0,

    /// <summary>Job is currently being processed.</summary>
    Running = 1,

    /// <summary>Job completed successfully.</summary>
    Completed = 2,

    /// <summary>Job failed during processing.</summary>
    Failed = 3,

    /// <summary>Job was cancelled before completion.</summary>
    Cancelled = 4
}

/// <summary>
/// Information about an enqueued thumbnail generation job.
/// </summary>
public sealed record ThumbnailJobInfo
{
    /// <summary>The unique job identifier.</summary>
    public required Guid JobId { get; init; }

    /// <summary>The document this job generates a thumbnail for.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Current status of the job.</summary>
    public required ThumbnailJobStatus Status { get; init; }

    /// <summary>When the job was enqueued.</summary>
    public DateTime EnqueuedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the job started processing, if applicable.</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>When the job completed, if applicable.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Error message if the job failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Storage path of the generated thumbnail, if completed.</summary>
    public string? ThumbnailPath { get; init; }
}

/// <summary>
/// Service interface for generating and managing document thumbnails.
/// </summary>
public interface IDocumentThumbnailService
{
    /// <summary>
    /// Generates a thumbnail for a document and stores it in MinIO.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The storage path of the generated thumbnail.</returns>
    Task<string> GenerateThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates thumbnails for multiple documents.
    /// </summary>
    /// <param name="documentIds">The document IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping document IDs to their thumbnail paths.</returns>
    Task<Dictionary<Guid, string>> GenerateThumbnailsAsync(IEnumerable<Guid> documentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the thumbnail URL for a document (generates if not exists).
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The presigned URL for the thumbnail.</returns>
    Task<string?> GetThumbnailUrlAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the thumbnail for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a thumbnail exists for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> HasThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a thumbnail generation job for background processing. Long-running thumbnails
    /// (large PDFs, multi-page TIFFs) should use this instead of <see cref="GenerateThumbnailAsync"/>.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unique job identifier for tracking.</returns>
    Task<Guid> EnqueueThumbnailGenerationAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of an enqueued thumbnail generation job.
    /// </summary>
    /// <param name="jobId">The job identifier returned by <see cref="EnqueueThumbnailGenerationAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current status and details of the job.</returns>
    Task<ThumbnailJobStatus> GetThumbnailJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending or in-progress thumbnail generation job.
    /// </summary>
    /// <param name="jobId">The job identifier to cancel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelThumbnailJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}