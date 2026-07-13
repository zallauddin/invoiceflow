namespace InvoiceFlow.Core.Models.Compliance;

/// <summary>
/// Result of PEPPOL BIS 3.0 invoice validation.
/// Contains validation status, errors, warnings, and the timestamp of the check.
/// </summary>
public sealed record PeppolValidationResult
{
    /// <summary>Whether the invoice passed all mandatory PEPPOL BIS 3.0 validation rules.</summary>
    public bool IsValid { get; init; }

    /// <summary>List of validation error messages (mandatory rule violations).</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>List of validation warning messages (recommended rule deviations).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>UTC timestamp when the validation was performed.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
