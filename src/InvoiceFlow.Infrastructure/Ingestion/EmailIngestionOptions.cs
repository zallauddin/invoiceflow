namespace InvoiceFlow.Infrastructure.Ingestion;

/// <summary>
/// Configuration for IMAP-based email ingestion.
/// Bound from the "EmailIngestion" section in appsettings.json.
/// </summary>
public sealed class EmailIngestionOptions
{
    public const string SectionName = "EmailIngestion";

    /// <summary>IMAP server hostname (e.g. "imap.gmail.com").</summary>
    public string ImapServer { get; set; } = string.Empty;

    /// <summary>IMAP server port (default 993 for SSL).</summary>
    public int ImapPort { get; set; } = 993;

    /// <summary>Username or email address for IMAP authentication.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password or app-specific password for IMAP authentication.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Whether to use SSL/TLS for the IMAP connection.</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>IMAP folder to monitor (e.g. "INBOX").</summary>
    public string Folder { get; set; } = "INBOX";

    /// <summary>Interval in minutes between email polls.</summary>
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// File extensions to accept as invoice attachments.
    /// Defaults to common invoice document formats.
    /// </summary>
    public HashSet<string> AttachmentExtensions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".xml", ".jpg", ".png", ".tiff"
        };

    /// <summary>MinIO bucket name for storing ingested attachments.</summary>
    public string BucketName { get; set; } = "documents";

    /// <summary>Tenant ID to assign to ingested documents and invoices.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Maximum number of retry attempts for IMAP connection failures.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay in seconds for exponential backoff on retries.</summary>
    public int RetryBaseDelaySeconds { get; set; } = 5;
}
