namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Result of a ZATCA invoice clearance request.
/// Contains the clearance status, identifiers, QR code data, and timing information.
/// </summary>
public record ZatcaClearanceResult
{
    /// <summary>Whether the invoice was successfully cleared by ZATCA.</summary>
    public bool Cleared { get; init; }

    /// <summary>Unique clearance identifier returned by ZATCA (UUID). Null if not cleared.</summary>
    public string? ClearanceId { get; init; }

    /// <summary>Base64-encoded TLV QR code representing the cleared invoice data.</summary>
    public string? QrCodeBase64 { get; init; }

    /// <summary>Error message from the ZATCA API, if the clearance failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp of the clearance response.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>SHA-256 hash of the FATOORAH XML submitted for clearance.</summary>
    public string? InvoiceHash { get; init; }
}
