namespace InvoiceFlow.Core.Enums;

/// <summary>Status of an invoice in the processing pipeline.</summary>
public enum InvoiceStatus
{
    Draft = 0,
    Received = 1,
    Extracting = 2,
    Extracted = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    Processing = 7,
    Compliant = 8,
    NonCompliant = 9,
    Transmitted = 10,
    Failed = 11,
    Cancelled = 12
}
