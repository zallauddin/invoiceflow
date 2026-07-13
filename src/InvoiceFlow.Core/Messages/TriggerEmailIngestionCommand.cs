namespace InvoiceFlow.Core.Messages;

/// <summary>
/// Command that triggers the email ingestion pipeline to poll the IMAP mailbox
/// for new invoice attachments and process them through the extraction pipeline.
/// </summary>
public sealed record TriggerEmailIngestionCommand
{
    public Guid TenantId { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
