using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// FTP/SFTP-based file ingestion service that polls a remote directory for invoice files,
/// downloads them to object storage, and creates Document entities for downstream extraction.
/// </summary>
public interface IFtpSftpIngestionService
{
    /// <summary>
    /// Polls the configured remote directory for matching invoice files,
    /// downloads and processes each file, and returns a summary of the operation.
    /// </summary>
    Task<FtpSftpIngestionResult> PollAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads and processes a single remote file by its path.
    /// </summary>
    Task<FtpSftpIngestionResult> ProcessFileAsync(string remotePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an FTP/SFTP ingestion operation.
/// </summary>
public sealed record FtpSftpIngestionResult
{
    /// <summary>Whether the operation completed without fatal errors.</summary>
    public bool Success { get; init; }

    /// <summary>Number of files successfully processed.</summary>
    public int ProcessedCount { get; init; }

    /// <summary>Number of files that failed processing.</summary>
    public int FailedCount { get; init; }

    /// <summary>Error message if the overall operation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Documents created from successfully processed files.</summary>
    public List<Document> CreatedDocuments { get; init; } = [];
}
