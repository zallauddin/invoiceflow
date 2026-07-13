namespace InvoiceFlow.Core.Enums;

/// <summary>Status of an approval request for an invoice.</summary>
public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Escalated = 3
}
