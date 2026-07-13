using InvoiceFlow.Core.Interfaces;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="IDocumentThumbnailService"/>. Actual thumbnail generation
/// (PDF/image → PNG) is not yet implemented — methods throw NotImplementedException.
/// </summary>
public sealed class DocumentThumbnailService : IDocumentThumbnailService
{
    public Task<string> GenerateThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail generation is not yet implemented.");

    public Task<Dictionary<Guid, string>> GenerateThumbnailsAsync(IEnumerable<Guid> documentIds, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Bulk thumbnail generation is not yet implemented.");

    public Task<string?> GetThumbnailUrlAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail URL retrieval is not yet implemented.");

    public Task DeleteThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail deletion is not yet implemented.");

    public Task<bool> HasThumbnailAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail existence check is not yet implemented.");

    public Task<Guid> EnqueueThumbnailGenerationAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail job enqueue is not yet implemented.");

    public Task<ThumbnailJobStatus> GetThumbnailJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail job status check is not yet implemented.");

    public Task CancelThumbnailJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Thumbnail job cancellation is not yet implemented.");
}
