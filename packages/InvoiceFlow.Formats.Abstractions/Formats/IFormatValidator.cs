namespace InvoiceFlow.Formats.Abstractions;

/// <summary>Validates invoice content against format-specific rules.</summary>
public interface IFormatValidator
{
    /// <summary>The format this validator supports.</summary>
    InvoiceFormat SupportedFormat { get; }

    /// <summary>Validate invoice content against format-specific business rules.</summary>
    Task<FormatValidationResult> ValidateAsync(Stream content, CancellationToken ct = default);
}

/// <summary>Result of validating invoice content against format rules.</summary>
public sealed record FormatValidationResult(
    bool IsValid,
    List<ValidationResult> Results,
    InvoiceFormat DetectedFormat
);
