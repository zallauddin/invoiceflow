namespace InvoiceFlow.Core.Messages;

public sealed record ExtractInvoiceCommand
{
    public Guid InvoiceId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public int Priority { get; init; } = 0;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public int RetryCount { get; init; } = 0;
}
