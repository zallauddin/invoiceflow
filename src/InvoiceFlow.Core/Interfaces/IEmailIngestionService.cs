using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// IMAP-based email ingestion service that polls a mailbox for invoice attachments,
/// stores them in object storage, and creates Document entities for downstream extraction.
/// </summary>
public interface IEmailIngestionService
{
    /// <summary>
    /// Polls the configured IMAP mailbox for unread messages with invoice attachments,
    /// processes each attachment, and returns a summary of the operation.
    /// </summary>
    Task<EmailIngestionResult> PollEmailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a single raw email message, extracting and storing any invoice attachments.
    /// </summary>
    Task<EmailIngestionResult> ProcessEmailAsync(string emailContent, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an email ingestion operation.
/// </summary>
public sealed record EmailIngestionResult
{
    /// <summary>Whether the operation completed without fatal errors.</summary>
    public bool Success { get; init; }

    /// <summary>Number of attachments successfully processed.</summary>
    public int ProcessedCount { get; init; }

    /// <summary>Number of attachments that failed processing.</summary>
    public int FailedCount { get; init; }

    /// <summary>Error message if the overall operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Documents created from successfully processed attachments.</summary>
    public List<Document> CreatedDocuments { get; init; } = [];
}
