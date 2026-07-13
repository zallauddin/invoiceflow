using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Reminder entity — represents a payment reminder or dunning letter.</summary>
public class Reminder : DocumentEntity
{
    /// <summary>Type of business document.</summary>
    public override DocumentType DocumentType => DocumentType.Reminder;

    /// <summary>Invoice this reminder references.</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>Reminder level (1 = first reminder, 2 = second, 3 = final).</summary>
    public int ReminderLevel { get; set; } = 1;

    /// <summary>Days overdue when this reminder was sent.</summary>
    public int DaysOverdue { get; set; }

    /// <summary>Reminder fee charged, if applicable.</summary>
    public decimal? ReminderFee { get; set; }

    /// <summary>Reminder-specific status (shadows DocumentEntity.Status with ReminderStatus).</summary>
    public new ReminderStatus Status { get; set; } = ReminderStatus.Pending;

    /// <summary>When the reminder payment is due.</summary>
    public new DateTime? DueDate { get; set; }

    /// <summary>When the reminder was sent to recipient.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>When recipient acknowledged the reminder.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Navigation property to the invoice.</summary>
    public Invoice Invoice { get; set; } = null!;
}