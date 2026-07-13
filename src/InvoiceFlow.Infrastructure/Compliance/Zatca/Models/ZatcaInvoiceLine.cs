namespace InvoiceFlow.Infrastructure.Compliance.Zatca.Models;

/// <summary>
/// Represents a single line item in a ZATCA FATOORAH XML invoice.
/// </summary>
public sealed class ZatcaInvoiceLine
{
    /// <summary>Sequential line number (1-based).</summary>
    public required int Id { get; init; }

    /// <summary>Description of the goods or services.</summary>
    public required string Description { get; init; }

    /// <summary>Quantity of goods or services.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Price per unit (tax-exclusive).</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Total amount for this line (Quantity * UnitPrice, tax-exclusive).</summary>
    public required decimal LineAmount { get; init; }

    /// <summary>VAT amount for this line.</summary>
    public required decimal VatAmount { get; init; }

    /// <summary>VAT rate applied to this line (percentage).</summary>
    public required decimal VatRate { get; init; }
}
