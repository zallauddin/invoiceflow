namespace InvoiceFlow.Core.Events;

/// <summary>Domain events for the credit note lifecycle.</summary>
public sealed record CreditNoteCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(CreditNoteCreatedEvent);
    public required Guid CreditNoteId { get; init; }
    public required Guid TenantId { get; init; }
    public required string DocumentNumber { get; init; }
    public Guid? OriginalInvoiceId { get; init; }
}

public sealed record CreditNoteUpdatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(CreditNoteUpdatedEvent);
    public required Guid CreditNoteId { get; init; }
    public required Guid TenantId { get; init; }
}

public sealed record CreditNoteCancelledEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(CreditNoteCancelledEvent);
    public required Guid CreditNoteId { get; init; }
    public required Guid TenantId { get; init; }
    public string? Reason { get; init; }
}

/// <summary>Domain events for the debit note lifecycle.</summary>
public sealed record DebitNoteCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(DebitNoteCreatedEvent);
    public required Guid DebitNoteId { get; init; }
    public required Guid TenantId { get; init; }
    public required string DocumentNumber { get; init; }
    public Guid? OriginalInvoiceId { get; init; }
}

public sealed record DebitNoteUpdatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(DebitNoteUpdatedEvent);
    public required Guid DebitNoteId { get; init; }
    public required Guid TenantId { get; init; }
}

/// <summary>Domain events for the purchase order lifecycle.</summary>
public sealed record PurchaseOrderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(PurchaseOrderCreatedEvent);
    public required Guid PurchaseOrderId { get; init; }
    public required Guid TenantId { get; init; }
    public required string DocumentNumber { get; init; }
}

public sealed record PurchaseOrderUpdatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(PurchaseOrderUpdatedEvent);
    public required Guid PurchaseOrderId { get; init; }
    public required Guid TenantId { get; init; }
}

public sealed record PurchaseOrderConfirmedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(PurchaseOrderConfirmedEvent);
    public required Guid PurchaseOrderId { get; init; }
    public required Guid TenantId { get; init; }
    public Guid? ConfirmedBy { get; init; }
}

/// <summary>Domain events for the delivery note lifecycle.</summary>
public sealed record DeliveryNoteCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(DeliveryNoteCreatedEvent);
    public required Guid DeliveryNoteId { get; init; }
    public required Guid TenantId { get; init; }
    public required string DocumentNumber { get; init; }
    public Guid? PurchaseOrderId { get; init; }
}

public sealed record DeliveryNoteDeliveredEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(DeliveryNoteDeliveredEvent);
    public required Guid DeliveryNoteId { get; init; }
    public required Guid TenantId { get; init; }
    public required DateTime DeliveredAt { get; init; }
    public string? ReceivedBy { get; init; }
}

/// <summary>Domain events for the payment reminder lifecycle.</summary>
public sealed record ReminderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(ReminderCreatedEvent);
    public required Guid ReminderId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required int ReminderLevel { get; init; }
    public required int DaysOverdue { get; init; }
}

public sealed record ReminderSentEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(ReminderSentEvent);
    public required Guid ReminderId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required DateTime SentAt { get; init; }
    public required int ReminderLevel { get; init; }
}

public sealed record ReminderEscalatedEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType => nameof(ReminderEscalatedEvent);
    public required Guid ReminderId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required int NewLevel { get; init; }
}
