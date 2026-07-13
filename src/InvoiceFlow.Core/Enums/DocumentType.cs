namespace InvoiceFlow.Core.Enums;

/// <summary>Type of business document.</summary>
public enum DocumentType
{
    Invoice = 0,
    CreditNote = 1,
    DebitNote = 2,
    PurchaseOrder = 3,
    DeliveryNote = 4,
    Reminder = 5
}
