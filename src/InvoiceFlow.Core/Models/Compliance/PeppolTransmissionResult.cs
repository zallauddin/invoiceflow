namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Result of transmitting a PEPPOL-compliant document to an Access Point.
/// Contains success status, transmission identifier, and error details.
/// </summary>
public sealed record PeppolTransmissionResult
{
    /// <summary>Whether the transmission was accepted by the Access Point.</summary>
    public bool Success { get; init; }

    /// <summary>Unique transmission identifier assigned by the Access Point, if successful.</summary>
    public string? TransmissionId { get; init; }

    /// <summary>Error message if the transmission failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the transmission was attempted.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
