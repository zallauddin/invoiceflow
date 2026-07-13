namespace InvoiceFlow.Formats.Abstractions;

/// <summary>A single validation rule result from format validation.</summary>
public sealed record ValidationResult(
    string RuleId,
    string Message,
    ValidationSeverity Severity,
    string? XPath = null,
    string? Value = null
);

/// <summary>Severity levels for validation results.</summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}
