namespace InvoiceFlow.Core.Enums;

/// <summary>
/// Status of a payment reminder.
/// </summary>
public enum ReminderStatus
{
    Pending = 0,
    Sent = 1,
    Acknowledged = 2,
    Paid = 3,
    Escalated = 4,
    Cancelled = 5
}
