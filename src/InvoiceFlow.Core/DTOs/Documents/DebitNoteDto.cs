using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Response DTO for debit note documents.</summary>
public sealed record DebitNoteDto
{
    /// <summary>Unique document identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tenant this document belongs to.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Document number as assigned by the issuer.</summary>
    public required string DocumentNumber { get; init; }

    /// <summary>Type of business document.</summary>
    public required DocumentType DocumentType { get; init; }

    /// <summary>Date of issue on the document.</summary>
    public DateTime? DocumentDate { get; init; }

    /// <summary>Payment due date, if specified.</summary>
    public DateTime? DueDate { get; init; }

    /// <summary>Name of the issuer/sender.</summary>
    public required string IssuerName { get; init; }

    /// <summary>Tax identification number of the issuer.</summary>
    public string? IssuerTaxId { get; init; }

    /// <summary>Email address of the issuer.</summary>
    public string? IssuerEmail { get; init; }

    /// <summary>Address of the issuer (JSON serialized).</summary>
    public string? IssuerAddress { get; init; }

    /// <summary>Name of the recipient/receiver.</summary>
    public required string RecipientName { get; init; }

    /// <summary>Tax identification number of the recipient.</summary>
    public string? RecipientTaxId { get; init; }

    /// <summary>Email address of the recipient.</summary>
    public string? RecipientEmail { get; init; }

    /// <summary>Address of the recipient (JSON serialized).</summary>
    public string? RecipientAddress { get; init; }

    /// <summary>Currency code (ISO 4217, 3 letters).</summary>
    public required string Currency { get; init; }

    /// <summary>Sum of line item totals before tax.</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Total tax amount across all line items.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Grand total amount (Subtotal + TaxAmount).</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Discount amount applied to the document, if any.</summary>
    public decimal? DiscountAmount { get; init; }

    /// <summary>Shipping/handling charges, if any.</summary>
    public decimal? ShippingAmount { get; init; }

    /// <summary>Current status in the processing pipeline.</summary>
    public required DocumentStatus Status { get; init; }

    /// <summary>Source from which the document was received.</summary>
    public IngestionSource Source { get; init; }

    /// <summary>Method used to extract data from the document.</summary>
    public ExtractionMethod? ExtractionMethod { get; init; }

    /// <summary>OCR extraction confidence score (0.0 - 1.0).</summary>
    public double? OcrConfidence { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code for compliance routing.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Compliance model applied to this document.</summary>
    public ComplianceModel? ComplianceModel { get; init; }

    /// <summary>External compliance reference identifier (e.g., IRN, UUID).</summary>
    public string? ComplianceId { get; init; }

    /// <summary>JSON response from the compliance provider.</summary>
    public string? ComplianceResponse { get; init; }

    /// <summary>Original filename of the uploaded document.</summary>
    public string? OriginalFileName { get; init; }

    /// <summary>MinIO storage path for the document.</summary>
    public string? StoragePath { get; init; }

    /// <summary>MIME type of the original document.</summary>
    public string? MimeType { get; init; }

    /// <summary>External reference number (PO number, contract reference, original invoice number).</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Free-text notes attached to the document.</summary>
    public string? Notes { get; init; }

    /// <summary>Document identifier in the connected ERP system.</summary>
    public string? ErpId { get; init; }

    /// <summary>UTC timestamp when the document record was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>UTC timestamp of the last update.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>UTC timestamp when data extraction completed.</summary>
    public DateTime? ExtractedAt { get; init; }

    /// <summary>UTC timestamp when compliance was achieved.</summary>
    public DateTime? CompliantAt { get; init; }

    /// <summary>UTC timestamp when the document was transmitted.</summary>
    public DateTime? TransmittedAt { get; init; }

    /// <summary>Original invoice this debit note references, if applicable.</summary>
    public Guid? OriginalInvoiceId { get; init; }

    /// <summary>Reason for the debit note (e.g., Price Increase, Additional Charges).</summary>
    public string? Reason { get; init; }
}
