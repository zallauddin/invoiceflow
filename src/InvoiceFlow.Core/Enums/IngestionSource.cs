namespace InvoiceFlow.Core.Enums;

/// <summary>Source from which an invoice document was received.</summary>
public enum IngestionSource
{
    Email = 0,
    Ftp = 1,
    Sftp = 2,
    ApiUpload = 3,
    Webhook = 4,
    Manual = 5
}
