using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>
/// Base abstract class for all business document types.
/// Contains shared properties and behavior for Invoice, CreditNote, DebitNote, PurchaseOrder, DeliveryNote, Reminder.
/// </summary>
public abstract class DocumentEntity
{
    /// <summary>Unique document identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this document belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Document number as assigned by the issuer.</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>Type of business document.</summary>
    public abstract DocumentType DocumentType { get; }

    /// <summary>Date of issue on the document.</summary>
    public DateTime DocumentDate { get; set; }

    /// <summary>Payment due date, if specified.</summary>
    public DateTime? DueDate { get; set; }

    // --- Party Information (Vendor/Supplier) ---
    /// <summary>Name of the issuer/sender (vendor for invoices, buyer for orders).</summary>
    public string IssuerName { get; set; } = string.Empty;

    /// <summary>Tax identification number of the issuer.</summary>
    public string? IssuerTaxId { get; set; }

    /// <summary>Email address of the issuer.</summary>
    public string? IssuerEmail { get; set; }

    /// <summary>Address of the issuer (JSON serialized).</summary>
    public string? IssuerAddress { get; set; }

    // --- Party Information (Recipient/Buyer) ---
    /// <summary>Name of the recipient/receiver (buyer for invoices, vendor for orders).</summary>
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>Tax identification number of the recipient.</summary>
    public string? RecipientTaxId { get; set; }

    /// <summary>Email address of the recipient.</summary>
    public string? RecipientEmail { get; set; }

    /// <summary>Address of the recipient (JSON serialized).</summary>
    public string? RecipientAddress { get; set; }

    // --- Financials ---
    /// <summary>Currency code (ISO 4217, 3 letters).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Sum of line item totals before tax.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Total tax amount across all line items.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Grand total amount (Subtotal + TaxAmount).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Discount amount applied to the document, if any.</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Shipping/handling charges, if any.</summary>
    public decimal? ShippingAmount { get; set; }

    // --- Status & Processing ---
    /// <summary>Current status in the processing pipeline.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    /// <summary>Source from which the document was received.</summary>
    public IngestionSource Source { get; set; }

    /// <summary>Method used to extract data from the document.</summary>
    public ExtractionMethod? ExtractionMethod { get; set; }

    /// <summary>OCR extraction confidence score (0.0 - 1.0).</summary>
    public double? OcrConfidence { get; set; }

    // --- Compliance ---
    /// <summary>ISO 3166-1 alpha-2 country code for compliance routing.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Compliance model applied to this document.</summary>
    public ComplianceModel? ComplianceModel { get; set; }

    /// <summary>External compliance reference identifier (e.g., IRN, UUID).</summary>
    public string? ComplianceId { get; set; }

    /// <summary>JSON response from the compliance provider.</summary>
    public string? ComplianceResponse { get; set; }

    // --- Document ---
    /// <summary>Original filename of the uploaded document.</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>MinIO storage path for the document.</summary>
    public string? StoragePath { get; set; }

    /// <summary>MIME type of the original document.</summary>
    public string? MimeType { get; set; }

    /// <summary>External reference number (PO number, contract reference, original invoice number).</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Free-text notes attached to the document.</summary>
    public string? Notes { get; set; }

    /// <summary>Document identifier in the connected ERP system.</summary>
    public string? ErpId { get; set; }

    // --- Timestamps ---
    /// <summary>UTC timestamp when the document record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when data extraction completed.</summary>
    public DateTime? ExtractedAt { get; set; }

    /// <summary>UTC timestamp when compliance was achieved.</summary>
    public DateTime? CompliantAt { get; set; }

    /// <summary>UTC timestamp when the document was transmitted.</summary>
    public DateTime? TransmittedAt { get; set; }

    // --- Concurrency Control ---
    /// <summary>Row version for optimistic concurrency control. EF Core maps this to a PostgreSQL xmin or bytea column.</summary>
    public byte[]? RowVersion { get; set; }

    // --- Soft Delete ---
    /// <summary>Soft-delete flag. When true, the document is marked deleted but retained for audit.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the document was soft-deleted, if applicable.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>User who soft-deleted the document, or null if not deleted or deleted by system.</summary>
    public Guid? DeletedByUserId { get; set; }

    // --- Navigation Properties ---
    /// <summary>Line items on this document.</summary>
    public List<DocumentLine> Lines { get; set; } = new();

    /// <summary>Tenant navigation property.</summary>
    public Tenant Tenant { get; set; } = null!;

    // --- Domain Events ---
    /// <summary>Domain events raised by this entity (not persisted).</summary>
    public List<IDomainEvent> DomainEvents { get; private set; } = new();

    /// <summary>Clears all pending domain events after they have been dispatched.</summary>
    public void ClearDomainEvents() => DomainEvents.Clear();
}