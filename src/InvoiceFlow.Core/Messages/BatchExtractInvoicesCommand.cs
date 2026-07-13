namespace InvoiceFlow.Core.Messages;

public sealed record BatchExtractInvoicesCommand
{
    public Guid[] InvoiceIds { get; init; } = [];
    public Guid TenantId { get; init; }
    public int Priority { get; init; } = 0;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
