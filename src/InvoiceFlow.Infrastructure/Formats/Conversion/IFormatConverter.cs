using InvoiceFlow.Core.Entities;
using InvoiceFlow.Formats.Abstractions;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// Converts invoice data between two electronic formats using the format registry.
/// The source format is auto-detected from the input stream.
/// </summary>
public interface IFormatConverter
{
    /// <summary>The detected source format (auto-detected at conversion time).</summary>
    InvoiceFormat SourceFormat { get; }

    /// <summary>The target format to convert to.</summary>
    InvoiceFormat TargetFormat { get; }

    /// <summary>
    /// Convert invoice content from the auto-detected source format to the target format.
    /// </summary>
    Task<FormatConversionResult> ConvertAsync(Stream source, CancellationToken ct = default);
}

/// <summary>
/// Result of converting invoice data between two formats.
/// </summary>
public sealed record FormatConversionResult
{
    /// <summary>Whether the conversion succeeded.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>The converted document stream (null on failure).</summary>
    public Stream? Output { get; init; }

    /// <summary>Intermediate invoice data extracted from the source.</summary>
    public required Invoice Invoice { get; init; }

    /// <summary>Intermediate line items extracted from the source.</summary>
    public required List<InvoiceLine> Lines { get; init; }

    /// <summary>The format detected from the source stream.</summary>
    public required InvoiceFormat DetectedSourceFormat { get; init; }

    /// <summary>Validation results from the source reader.</summary>
    public required List<ValidationResult> SourceValidationResults { get; init; }

    /// <summary>Validation results from the target writer.</summary>
    public required List<ValidationResult> TargetValidationResults { get; init; }

    /// <summary>Error message if conversion failed (null on success).</summary>
    public string? ErrorMessage { get; init; }
}
