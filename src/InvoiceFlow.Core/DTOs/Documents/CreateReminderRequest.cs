using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.DTOs.Documents;

/// <summary>Request payload for creating a new reminder.</summary>
public sealed record CreateReminderRequest
{
    /// <summary>Document number as assigned by the issuer.</summary>
    public required string DocumentNumber { get; init; }

    /// <summary>Date of issue on the document.</summary>
    public DateTime? DocumentDate { get; init; }

    /// <summary>When the reminder payment is due.</summary>
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

    /// <summary>Invoice this reminder references (required).</summary>
    public required Guid InvoiceId { get; init; }

    /// <summary>Reminder level (1 = first reminder, 2 = second, 3 = final). Default: 1.</summary>
    public int ReminderLevel { get; init; } = 1;

    /// <summary>Days overdue when this reminder was sent.</summary>
    public int DaysOverdue { get; init; }

    /// <summary>Reminder fee charged, if applicable.</summary>
    public decimal? ReminderFee { get; init; }
}
