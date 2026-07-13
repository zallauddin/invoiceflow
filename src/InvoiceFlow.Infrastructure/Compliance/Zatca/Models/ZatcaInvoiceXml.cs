namespace InvoiceFlow.Infrastructure.Compliance.Zatca.Models;

/// <summary>
/// Represents the data structure for a ZATCA FATOORAH XML invoice.
/// Maps to the UBL 2.1-based XML schema required by the Saudi ZATCA e-invoicing system.
/// </summary>
public sealed class ZatcaInvoiceXml
{
    /// <summary>Invoice number as assigned by the supplier.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>Invoice issue date in ISO 8601 format (yyyy-MM-ddTHH:mm:ssZ).</summary>
    public required DateTime InvoiceDate { get; init; }

    /// <summary>Legal name of the supplier (seller).</summary>
    public required string SupplierName { get; init; }

    /// <summary>VAT registration number of the supplier.</summary>
    public required string SupplierVatNumber { get; init; }

    /// <summary>Legal name of the customer (buyer).</summary>
    public required string CustomerName { get; init; }

    /// <summary>VAT registration number of the customer. May be null for B2C transactions.</summary>
    public string? CustomerVatNumber { get; init; }

    /// <summary>Grand total amount including VAT.</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>Total tax-exclusive amount before VAT.</summary>
    public required decimal SubtotalAmount { get; init; }

    /// <summary>Total VAT amount across all line items.</summary>
    public required decimal TaxAmount { get; init; }

    /// <summary>Total VAT amount (alias for TaxAmount, included for TLV QR code mapping).</summary>
    public required decimal VatAmount { get; init; }

    /// <summary>VAT rate as a percentage (default 15% for Saudi Arabia).</summary>
    public decimal VatRate { get; init; } = 15.0m;

    /// <summary>Currency code (ISO 4217). Defaults to SAR.</summary>
    public string Currency { get; init; } = "SAR";

    /// <summary>Line items on the invoice.</summary>
    public required List<ZatcaInvoiceLine> Lines { get; init; } = new();
}
