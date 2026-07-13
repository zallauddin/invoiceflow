using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Entities;

/// <summary>Delivery note entity — represents a proof of delivery for goods/services.</summary>
public class DeliveryNote : DocumentEntity
{
    /// <summary>Type of business document.</summary>
    public override DocumentType DocumentType => DocumentType.DeliveryNote;

    /// <summary>Purchase order this delivery note references, if applicable.</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Delivery address (JSON serialized).</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Name of the carrier/transporter.</summary>
    public string? CarrierName { get; set; }

    /// <summary>Tracking number for the shipment.</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>Date when delivery was completed.</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Name of the person who received the delivery.</summary>
    public string? ReceivedBy { get; set; }

    /// <summary>Total quantity delivered (decimal 18,2).</summary>
    public decimal? DeliveredQuantity { get; set; }

    /// <summary>Path to signature image (max 1000).</summary>
    public string? SignaturePath { get; set; }

    /// <summary>Path to proof-of-delivery image (max 1000).</summary>
    public string? ProofOfDeliveryPath { get; set; }

    /// <summary>When physically received (distinct from DeliveredAt which is carrier drop-off).</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Base64-encoded signature data (no length limit).</summary>
    public string? ReceiverSignature { get; set; }

    /// <summary>Navigation property to the purchase order.</summary>
    public PurchaseOrder? PurchaseOrder { get; set; }
}