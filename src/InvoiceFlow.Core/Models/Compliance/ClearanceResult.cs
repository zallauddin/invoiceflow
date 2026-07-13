namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Represents the result of a country-specific e-invoicing compliance clearance operation.
/// </summary>
public record ClearanceResult
{
    /// <summary>Whether the invoice was successfully cleared.</summary>
    public bool Cleared { get; init; }

    /// <summary>External compliance reference identifier (e.g., IRN, UUID, protocol number), if cleared.</summary>
    public string? ClearanceId { get; init; }

    /// <summary>Error message if clearance failed, or <c>null</c> on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the clearance operation was executed.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Raw provider response payload for auditing and diagnostics.</summary>
    public string? ProviderResponse { get; init; }
}
