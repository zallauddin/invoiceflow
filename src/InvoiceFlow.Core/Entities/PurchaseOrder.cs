using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Purchase order entity — represents a buyer's order to a vendor.</summary>
public class PurchaseOrder : DocumentEntity
{
    /// <summary>Type of business document.</summary>
    public override DocumentType DocumentType => DocumentType.PurchaseOrder;

    /// <summary>Expected delivery date for the order.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Delivery address (JSON serialized).</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Payment terms (e.g., Net 30, Net 60).</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Incoterms (e.g., FOB, CIF, EXW).</summary>
    public string? Incoterms { get; set; }

    /// <summary>Ship-to recipient name (max 300).</summary>
    public string? ShipToName { get; set; }

    /// <summary>Ship-to address (no max length).</summary>
    public string? ShipToAddress { get; set; }

    /// <summary>Bill-to recipient name (max 300).</summary>
    public string? BillToName { get; set; }

    /// <summary>Bill-to address (no max length).</summary>
    public string? BillToAddress { get; set; }

    /// <summary>Contact person name (max 300).</summary>
    public string? ContactName { get; set; }

    /// <summary>Contact email (max 255).</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Contact phone (max 50).</summary>
    public string? ContactPhone { get; set; }
}