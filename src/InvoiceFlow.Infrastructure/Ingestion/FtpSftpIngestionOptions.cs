namespace InvoiceFlow.Infrastructure.Ingestion;

/// <summary>
/// Configuration for FTP/SFTP-based file ingestion.
/// Bound from the "FtpSftpIngestion" section in appsettings.json.
/// </summary>
public sealed class FtpSftpIngestionOptions
{
    public const string SectionName = "FtpSftpIngestion";

    /// <summary>Protocol to use: "FTP", "FTPS", or "SFTP".</summary>
    public string Protocol { get; init; } = "SFTP";

    /// <summary>Server hostname.</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>Server port (default 22 for SFTP, 21 for FTP).</summary>
    public int Port { get; init; } = 22;

    /// <summary>Username for authentication.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Password for authentication.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Optional path to SSH private key file for SFTP key-based auth.</summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>Optional passphrase for the SSH private key.</summary>
    public string? Passphrase { get; init; }

    /// <summary>Remote directory to poll for incoming files.</summary>
    public string RemotePath { get; init; } = "/incoming";

    /// <summary>Remote directory to move successfully processed files.</summary>
    public string ProcessedPath { get; init; } = "/processed";

    /// <summary>Remote directory to move files that failed processing.</summary>
    public string FailedPath { get; init; } = "/failed";

    /// <summary>File extensions to accept (case-insensitive).</summary>
    public string[] FileExtensions { get; init; } = [".pdf", ".xml", ".jpg", ".png", ".tiff"];

    /// <summary>Interval in minutes between polls.</summary>
    public int PollIntervalMinutes { get; init; } = 5;

    /// <summary>Tenant ID to assign to ingested documents and invoices.</summary>
    public Guid TenantId { get; init; }

    /// <summary>MinIO bucket name for storing ingested files.</summary>
    public string BucketName { get; init; } = "documents";

    /// <summary>Maximum number of retry attempts for connection failures.</summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>Base delay in seconds for exponential backoff on retries.</summary>
    public int RetryBaseDelaySeconds { get; init; } = 5;
}
