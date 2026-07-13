using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Credit note entity — represents a credit memo issued by a vendor to a buyer.</summary>
public class CreditNote : DocumentEntity
{
    /// <summary>Type of business document.</summary>
    public override DocumentType DocumentType => DocumentType.CreditNote;

    /// <summary>Original invoice this credit note references, if applicable.</summary>
    public Guid? OriginalInvoiceId { get; set; }

    /// <summary>Reason for the credit note (e.g., Return, Discount, Error Correction).</summary>
    public string? Reason { get; set; }

    /// <summary>Navigation property to the original invoice.</summary>
    public Invoice? OriginalInvoice { get; set; }
}
