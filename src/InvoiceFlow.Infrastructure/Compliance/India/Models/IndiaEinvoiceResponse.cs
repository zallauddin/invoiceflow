using System.Text.Json.Serialization;

namespace InvoiceFlow.Infrastructure.Compliance.India.Models;

/// <summary>
/// GSTN e-Invoice response from the IRP (Invoice Registration Portal).
/// </summary>
public class IndiaEinvoiceResponse
{
    /// <summary>Acknowledgement number from the IRP.</summary>
    [JsonPropertyName("AckNo")]
    public long AckNo { get; set; }

    /// <summary>Acknowledgement date (dd/MM/yyyy HH:mm:ss).</summary>
    [JsonPropertyName("AckDt")]
    public string AckDt { get; set; } = string.Empty;

    /// <summary>Invoice Reference Number (IRN) — the unique hash-based identifier.</summary>
    [JsonPropertyName("Irn")]
    public string Irn { get; set; } = string.Empty;

    /// <summary>Signed invoice payload (base64-encoded).</summary>
    [JsonPropertyName("SignedInvoice")]
    public string SignedInvoice { get; set; } = string.Empty;

    /// <summary>Signed QR code payload (base64-encoded).</summary>
    [JsonPropertyName("SignedQrCode")]
    public string SignedQrCode { get; set; } = string.Empty;

    /// <summary>Status message from the IRP.</summary>
    [JsonPropertyName("Status")]
    public string Status { get; set; } = string.Empty;
}
