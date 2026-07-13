namespace InvoiceFlow.Core.Messages;

/// <summary>
/// Command that triggers the FTP/SFTP ingestion pipeline to poll the remote directory
/// for new invoice files and process them through the extraction pipeline.
/// </summary>
public sealed record TriggerFtpSftpIngestionCommand
{
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
