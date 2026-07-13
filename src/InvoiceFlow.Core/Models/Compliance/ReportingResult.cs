namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Unified result of a CTC (Continuous Transaction Control) real-time reporting submission
/// to a country-specific e-invoicing portal (SdI, PPF, or KSeF).
/// </summary>
public record ReportingResult
{
    /// <summary>Whether the report was accepted by the portal.</summary>
    public bool Accepted { get; init; }

    /// <summary>Portal-assigned reference identifier on acceptance, or <c>null</c> if rejected.</summary>
    public string? ReferenceId { get; init; }

    /// <summary>Error message if the report was rejected, or <c>null</c> on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the reporting response was received.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Raw provider response payload for auditing and diagnostics.</summary>
    public string? ProviderResponse { get; init; }
}
