namespace InvoiceFlow.Core.Enums;

/// <summary>Direction of data synchronization with an ERP system.</summary>
public enum SyncDirection
{
    Push = 0,
    Pull = 1,
    Bidirectional = 2
}
