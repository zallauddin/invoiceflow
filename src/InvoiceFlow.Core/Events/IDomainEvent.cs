namespace InvoiceFlow.Core.Events;

/// <summary>Marker interface for all domain events.</summary>
public interface IDomainEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>Timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Type name of the event for serialization.</summary>
    string EventType { get; }
}
