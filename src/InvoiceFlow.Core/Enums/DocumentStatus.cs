namespace InvoiceFlow.Core.Enums;

/// <summary>Status of a business document in the processing pipeline.</summary>
public enum DocumentStatus
{
    Draft = 0,
    Received = 1,
    Extracting = 2,
    Extracted = 3,
    Validating = 4,
    Validated = 5,
    Compliant = 6,
    Transmitted = 7,
    Rejected = 8,
    Archived = 9,
    Cancelled = 10
}