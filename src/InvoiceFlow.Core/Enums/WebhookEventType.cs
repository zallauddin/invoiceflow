namespace InvoiceFlow.Core.Enums;

/// <summary>Types of events that can trigger webhook notifications.</summary>
public enum WebhookEventType
{
    InvoiceReceived = 0,
    InvoiceExtracted = 1,
    InvoiceApproved = 2,
    InvoiceRejected = 3,
    InvoiceCompliant = 4,
    InvoiceTransmitted = 5,
    InvoiceFailed = 6,
    ComplianceProcessed = 7,
    ErpSynced = 8
}
