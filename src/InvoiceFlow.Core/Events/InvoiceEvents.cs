namespace InvoiceFlow.Core.Events;

/// <summary>Domain events for the invoice lifecycle.</summary>
public sealed record InvoiceReceivedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceReceivedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Source { get; init; }
    public required string FileName { get; init; }
}

public sealed record InvoiceExtractedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceExtractedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ExtractionMethod { get; init; }
    public required double Confidence { get; init; }
}

public sealed record InvoiceApprovedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceApprovedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApprovedByUserId { get; init; }
    public string? Comments { get; init; }
}

public sealed record InvoiceRejectedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceRejectedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid RejectedByUserId { get; init; }
    public required string Reason { get; init; }
}

public sealed record InvoiceCompliantEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceCompliantEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ComplianceModel { get; init; }
    public required string ComplianceId { get; init; }
}

public sealed record InvoiceTransmittedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceTransmittedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string TransmissionId { get; init; }
}

public sealed record InvoiceFailedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(InvoiceFailedEvent);
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string FailureReason { get; init; }
    public string? StackTrace { get; init; }
}
