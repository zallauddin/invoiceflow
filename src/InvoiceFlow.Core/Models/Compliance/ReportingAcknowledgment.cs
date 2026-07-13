namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Acknowledgment received from a CTC portal when checking the status of a previously submitted report.
/// </summary>
public record ReportingAcknowledgment
{
    /// <summary>Whether the report has been accepted and processed.</summary>
    public bool Accepted { get; init; }

    /// <summary>Portal-assigned reference identifier, or <c>null</c> if not yet assigned.</summary>
    public string? ReferenceId { get; init; }

    /// <summary>Error code returned by the portal, if applicable.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message returned by the portal, if applicable.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>UTC timestamp when the acknowledgment was received.</summary>
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}
