using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Request payload for creating a new delivery note.</summary>
public sealed record CreateDeliveryNoteRequest
{
    /// <summary>Document number as assigned by the issuer.</summary>
    public required string DocumentNumber { get; init; }

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
    public string Currency { get; init; } = "EUR";

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
    public DocumentStatus Status { get; init; } = DocumentStatus.Draft;

    /// <summary>Source from which the document was received.</summary>
    public IngestionSource Source { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code for compliance routing.</summary>
    public string? CountryCode { get; init; }

    /// <summary>External reference number (PO number, contract reference, original invoice number).</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Free-text notes attached to the document.</summary>
    public string? Notes { get; init; }

    /// <summary>Purchase order this delivery note references, if applicable.</summary>
    public Guid? PurchaseOrderId { get; init; }

    /// <summary>Delivery address (JSON serialized).</summary>
    public string? DeliveryAddress { get; init; }

    /// <summary>Name of the carrier/transporter.</summary>
    public string? CarrierName { get; init; }

    /// <summary>Tracking number for the shipment.</summary>
    public string? TrackingNumber { get; init; }

    /// <summary>Date when delivery was completed.</summary>
    public DateTime? DeliveredAt { get; init; }

    /// <summary>Name of the person who received the delivery.</summary>
    public string? ReceivedBy { get; init; }

    /// <summary>Total quantity delivered.</summary>
    public decimal? DeliveredQuantity { get; init; }

    /// <summary>Path to signature image.</summary>
    public string? SignaturePath { get; init; }

    /// <summary>Path to proof-of-delivery image.</summary>
    public string? ProofOfDeliveryPath { get; init; }

    /// <summary>When physically received (distinct from DeliveredAt which is carrier drop-off).</summary>
    public DateTime? ReceivedAt { get; init; }

    /// <summary>Base64-encoded signature data.</summary>
    public string? ReceiverSignature { get; init; }
}
