namespace InvoiceFlow.Infrastructure.Compliance.France.Models;

/// <summary>
/// Result of submitting an invoice to the French PPF (Portail Public de Facturation).
/// </summary>
public sealed record FrancePpfSubmissionResult
{
    /// <summary>Whether the PPF accepted the submission.</summary>
    public bool Accepted { get; init; }

    /// <summary>PPF-assigned reference number on acceptance, or <c>null</c> if rejected.</summary>
    public string? PpfReference { get; init; }

    /// <summary>Error code returned by PPF on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message returned by PPF on rejection, or <c>null</c> on acceptance.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp of the PPF response.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
