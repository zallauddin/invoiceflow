namespace InvoiceFlow.Core.Messages;

public sealed record ExtractionProgressEvent
{
    public Guid InvoiceId { get; init; }
    public Guid TenantId { get; init; }
    public string Stage { get; init; } = string.Empty; // "OCR", "LLM", "Template", "Complete", "Failed"
    public int Progress { get; init; } // 0-100
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
