using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Debit note entity — represents a debit memo issued by a buyer to a vendor.</summary>
public class DebitNote : DocumentEntity
{
    /// <summary>Type of business document.</summary>
    public override DocumentType DocumentType => DocumentType.DebitNote;

    /// <summary>Original invoice this debit note references, if applicable.</summary>
    public Guid? OriginalInvoiceId { get; set; }

    /// <summary>Reason for the debit note (e.g., Price Increase, Additional Charges).</summary>
    public string? Reason { get; set; }

    /// <summary>Navigation property to the original invoice.</summary>
    public Invoice? OriginalInvoice { get; set; }
}