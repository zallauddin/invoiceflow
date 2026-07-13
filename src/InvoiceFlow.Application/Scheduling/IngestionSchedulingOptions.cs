namespace InvoiceFlow.Application.Scheduling;

/// <summary>
/// Configuration for ingestion scheduling via Quartz.NET.
/// Bound from the "IngestionScheduling" section in appsettings.json.
/// </summary>
public sealed class IngestionSchedulingOptions
{
    public const string SectionName = "IngestionScheduling";

    /// <summary>
    /// Cron expression for email ingestion scheduling.
    /// Defaults to every 5 minutes: "0 */5 * * * ?".
    /// </summary>
    public string EmailCronExpression { get; set; } = "0 */5 * * * ?";

    /// <summary>
    /// Cron expression for FTP/SFTP ingestion scheduling.
    /// Defaults to every 10 minutes: "0 */10 * * * ?".
    /// </summary>
    public string FtpSftpCronExpression { get; set; } = "0 */10 * * * ?";

    /// <summary>
    /// Tenant ID to use for scheduled ingestion triggers.
    /// Each scheduled poll applies to a single tenant.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Whether email ingestion scheduling is enabled.
    /// </summary>
    public bool EmailEnabled { get; set; } = true;

    /// <summary>
    /// Whether FTP/SFTP ingestion scheduling is enabled.
    /// </summary>
    public bool FtpSftpEnabled { get; set; } = true;
}
