using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Unified result record that wraps all possible compliance outcomes
/// regardless of which country-specific service processed the invoice.
/// </summary>
public record ComplianceOrchestrationResult
{
    /// <summary>Whether the compliance operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The compliance model that was applied.</summary>
    public ComplianceModel? Model { get; init; }

    /// <summary>External compliance reference identifier (e.g., IRN, UUID, SdI number).</summary>
    public string? ComplianceId { get; init; }

    /// <summary>Error message if the operation failed, or <c>null</c> on success.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Raw provider response payload for auditing and diagnostics.</summary>
    public string? ProviderResponse { get; init; }

    /// <summary>UTC timestamp when the result was produced.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>SHA-256 archival hash for post-audit immutable records.</summary>
    public string? ArchivalHash { get; init; }
}
