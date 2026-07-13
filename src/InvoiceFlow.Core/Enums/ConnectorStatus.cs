namespace InvoiceFlow.Core.Enums;

/// <summary>Status of an ERP connector configuration.</summary>
public enum ConnectorStatus
{
    Active = 0,
    Inactive = 1,
    Error = 2,
    PendingAuth = 3
}
