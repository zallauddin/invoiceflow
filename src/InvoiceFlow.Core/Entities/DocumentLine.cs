namespace InvoiceFlow.Core.Entities;

/// <summary>A single line item on a business document (Invoice, CreditNote, PurchaseOrder, etc.).</summary>
public class DocumentLine
{
    /// <summary>Unique line item identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Document this line belongs to.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Sequential line number (1-based).</summary>
    public int LineNumber { get; set; }

    /// <summary>Description of the goods or services.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Product or item code (vendor's SKU, HSN code, etc.).</summary>
    public string? ProductCode { get; set; }

    /// <summary>Harmonized System Nomenclature code (for GST/customs).</summary>
    public string? HsnCode { get; set; }

    /// <summary>Quantity of goods or services.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of measure (EA, KG, H, M, etc.).</summary>
    public string? Unit { get; set; }

    /// <summary>Price per unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Total amount for this line (Quantity * UnitPrice).</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Tax rate applied to this line (percentage).</summary>
    public decimal TaxRate { get; set; }

    /// <summary>Tax amount for this line.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Tax category code (S, Z, E, AE, K, G, O, L, M for PEPPOL).</summary>
    public string? TaxCategory { get; set; }

    /// <summary>Discount percentage, if applicable.</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Discount amount, if applicable.</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Navigation property to the parent document.</summary>
    public DocumentEntity Document { get; set; } = null!;
}