namespace InvoiceFlow.Infrastructure.Compliance.Italy.Models;

/// <summary>
/// Result of transmitting an invoice to the Italian SdI (Sistema di Interscambio).
/// </summary>
public sealed record ItalySdiTransmissionResult
{
    /// <summary>Whether the SdI accepted the transmission.</summary>
    public bool Accepted { get; init; }

    /// <summary>Unique SdI identifier assigned on acceptance, or <c>null</c> if rejected.</summary>
    public string? SdiIdentifier { get; init; }

    /// <summary>Error code returned by SdI on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message returned by SdI on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp of the SdI response.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
