using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Entities;

/// <summary>Represents a document managed in the DMS (Document Management System).</summary>
public class Document
{
    /// <summary>Unique document identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this document belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Original filename of the document.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type of the document (e.g., application/pdf).</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>MinIO storage path for the document.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Extracted OCR text content, if available.</summary>
    public string? OcrText { get; set; }

    /// <summary>JSON array of tags for organization.</summary>
    public string? Tags { get; set; }

    /// <summary>Folder path for logical organization.</summary>
    public string? Folder { get; set; }

    /// <summary>Document version number (incremented on re-upload).</summary>
    public int Version { get; set; } = 1;

    /// <summary>Indicates if this is the latest version of the document.</summary>
    public bool IsLatestVersion { get; set; } = true;

    /// <summary>SHA-256 checksum of the document content for deduplication.</summary>
    public string? Checksum { get; set; }

    /// <summary>Number of pages in the document.</summary>
    public int? PageCount { get; set; }

    /// <summary>MinIO storage path for the document thumbnail.</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Full-text search vector for PostgreSQL GIN index.</summary>
    public string? SearchVector { get; set; }

    /// <summary>Optional link to an invoice entity.</summary>
    public Guid? LinkedInvoiceId { get; set; }

    /// <summary>Optional link to a credit note entity.</summary>
    public Guid? LinkedCreditNoteId { get; set; }

    /// <summary>Optional link to a debit note entity.</summary>
    public Guid? LinkedDebitNoteId { get; set; }

    /// <summary>Optional link to a purchase order entity.</summary>
    public Guid? LinkedPurchaseOrderId { get; set; }

    /// <summary>Optional link to a delivery note entity.</summary>
    public Guid? LinkedDeliveryNoteId { get; set; }

    /// <summary>Optional link to a reminder entity.</summary>
    public Guid? LinkedReminderId { get; set; }

    /// <summary>ID of the original document this version was derived from.</summary>
    public Guid? OriginalDocumentId { get; set; }

    /// <summary>Business document type.</summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>UTC timestamp when the document was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Navigation property to the linked invoice.</summary>
    public Invoice? LinkedInvoice { get; set; }

    /// <summary>Navigation property to the linked credit note.</summary>
    public CreditNote? LinkedCreditNote { get; set; }

    /// <summary>Navigation property to the linked debit note.</summary>
    public DebitNote? LinkedDebitNote { get; set; }

    /// <summary>Navigation property to the linked purchase order.</summary>
    public PurchaseOrder? LinkedPurchaseOrder { get; set; }

    /// <summary>Navigation property to the linked delivery note.</summary>
    public DeliveryNote? LinkedDeliveryNote { get; set; }

    /// <summary>Navigation property to the linked reminder.</summary>
    public Reminder? LinkedReminder { get; set; }

    /// <summary>Navigation property to the original document (for versioning).</summary>
    public Document? OriginalDocument { get; set; }

    /// <summary>Navigation property to all versions of this document.</summary>
    public List<Document> Versions { get; set; } = new();

    /// <summary>Navigation property to version history entries.</summary>
    public List<DocumentVersionHistory> VersionHistory { get; set; } = new();

    /// <summary>Navigation property to document relationships where this is the source.</summary>
    public List<DocumentRelationship> OutgoingRelationships { get; set; } = new();

    /// <summary>Navigation property to document relationships where this is the target.</summary>
    public List<DocumentRelationship> IncomingRelationships { get; set; } = new();
}