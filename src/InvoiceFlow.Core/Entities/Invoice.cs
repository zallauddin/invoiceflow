using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Core invoice entity — the central domain object in InvoiceFlow.</summary>
public class Invoice
{
    /// <summary>Unique invoice identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant this invoice belongs to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Invoice number as assigned by the vendor.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Type of business document.</summary>
    public DocumentType DocumentType { get; set; } = DocumentType.Invoice;

    /// <summary>Date of issue on the invoice.</summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>Payment due date, if specified.</summary>
    public DateTime? DueDate { get; set; }

    // --- Vendor (supplier) ---
    /// <summary>Name of the vendor/supplier.</summary>
    public string VendorName { get; set; } = string.Empty;

    /// <summary>Tax identification number of the vendor.</summary>
    public string? VendorTaxId { get; set; }

    /// <summary>Email address of the vendor.</summary>
    public string? VendorEmail { get; set; }

    // --- Buyer (customer) ---
    /// <summary>Name of the buyer/customer.</summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>Tax identification number of the buyer.</summary>
    public string? BuyerTaxId { get; set; }

    // --- Financials ---
    /// <summary>Currency code (ISO 4217, 3 letters).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Sum of line item totals before tax.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Total tax amount across all line items.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Grand total amount (Subtotal + TaxAmount).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Discount amount applied to the invoice, if any.</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Shipping/handling charges, if any.</summary>
    public decimal? ShippingAmount { get; set; }

    // --- Status & Processing ---
    /// <summary>Current status in the processing pipeline.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Source from which the invoice was received.</summary>
    public IngestionSource Source { get; set; }

    /// <summary>Method used to extract data from the document.</summary>
    public ExtractionMethod? ExtractionMethod { get; set; }

    /// <summary>OCR extraction confidence score (0.0 - 1.0).</summary>
    public double? OcrConfidence { get; set; }

    // --- Compliance ---
    /// <summary>ISO 3166-1 alpha-2 country code for compliance routing.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Compliance model applied to this invoice.</summary>
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

    /// <summary>External reference number (PO number, contract reference).</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Free-text notes attached to the invoice.</summary>
    public string? Notes { get; set; }

    /// <summary>Invoice identifier in the connected ERP system.</summary>
    public string? ErpId { get; set; }

    // --- Timestamps ---
    /// <summary>UTC timestamp when the invoice record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when data extraction completed.</summary>
    public DateTime? ExtractedAt { get; set; }

    /// <summary>UTC timestamp when compliance was achieved.</summary>
    public DateTime? CompliantAt { get; set; }

    /// <summary>UTC timestamp when the invoice was transmitted.</summary>
    public DateTime? TransmittedAt { get; set; }

    // --- Navigation Properties ---
    /// <summary>Line items on this invoice.</summary>
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>Tenant navigation property.</summary>
    public Tenant Tenant { get; set; } = null!;

    // --- Domain Events ---
    /// <summary>Domain events raised by this entity (not persisted).</summary>
    public List<IDomainEvent> DomainEvents { get; private set; } = new();

    /// <summary>Clears all pending domain events after they have been dispatched.</summary>
    public void ClearDomainEvents() => DomainEvents.Clear();
}
