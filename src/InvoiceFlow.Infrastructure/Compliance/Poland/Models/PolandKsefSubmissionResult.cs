namespace InvoiceFlow.Infrastructure.Compliance.Poland.Models;

/// <summary>
/// Result of submitting an invoice to the Polish KSeF (Krajowy System e-Faktur).
/// </summary>
public sealed record PolandKsefSubmissionResult
{
    /// <summary>Whether the KSeF accepted the submission.</summary>
    public bool Accepted { get; init; }

    /// <summary>KSeF-assigned reference number on acceptance, or <c>null</c> if rejected.</summary>
    public string? KsefReferenceNumber { get; init; }

    /// <summary>Error code returned by KSeF on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message returned by KSeF on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp of the KSeF response.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
