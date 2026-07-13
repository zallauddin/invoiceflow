using System.Diagnostics;
using InvoiceFlow.Formats.Abstractions;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// High-level pipeline for format conversion, batch processing, and format detection.
/// </summary>
public interface IFormatConversionPipeline
{
    /// <summary>
    /// Convert invoice content from auto-detected source format to the specified target format.
    /// </summary>
    Task<FormatConversionResult> ConvertAsync(
        Stream source,
        InvoiceFormat targetFormat,
        CancellationToken ct = default);

    /// <summary>
    /// Convert invoice content from a known source format to a target format.
    /// </summary>
    Task<FormatConversionResult> ConvertAsync(
        Stream source,
        InvoiceFormat sourceFormat,
        InvoiceFormat targetFormat,
        CancellationToken ct = default);

    /// <summary>
    /// Convert a batch of invoice items to a common target format.
    /// </summary>
    Task<BatchConversionResult> ConvertBatchAsync(
        BatchConversionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Detect the format of the provided content with a confidence score.
    /// </summary>
    Task<FormatDetectionResult> DetectFormatAsync(
        Stream content,
        string? fileName = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of format detection with a confidence score.
/// </summary>
public sealed record FormatDetectionResult
{
    /// <summary>The detected invoice format.</summary>
    public required InvoiceFormat DetectedFormat { get; init; }

    /// <summary>Descriptor for the detected format, if registered.</summary>
    public FormatDescriptor? Descriptor { get; init; }

    /// <summary>Confidence score from 0.0 (unknown) to 1.0 (certain).</summary>
    public required double Confidence { get; init; }
}

/// <summary>
/// Request for batch conversion of multiple invoice items.
/// </summary>
public sealed record BatchConversionRequest
{
    /// <summary>The items to convert.</summary>
    public required List<ConversionItem> Items { get; init; }

    /// <summary>The target format for all conversions.</summary>
    public required InvoiceFormat TargetFormat { get; init; }

    /// <summary>Whether to continue processing remaining items when one fails.</summary>
    public bool ContinueOnError { get; init; } = true;

    /// <summary>Whether to run source validation during conversion.</summary>
    public bool ValidateSource { get; init; } = false;

    /// <summary>Whether to run target validation during conversion.</summary>
    public bool ValidateTarget { get; init; } = false;
}

/// <summary>
/// A single item within a batch conversion request.
/// </summary>
public sealed record ConversionItem
{
    /// <summary>The invoice content stream.</summary>
    public required Stream Content { get; init; }

    /// <summary>Optional file name for format detection hints.</summary>
    public string? FileName { get; init; }

    /// <summary>Optional pre-known source format (skips detection when provided).</summary>
    public InvoiceFormat? KnownSourceFormat { get; init; }
}

/// <summary>
/// Aggregate result of processing a batch of conversion items.
/// </summary>
public sealed record BatchConversionResult
{
    /// <summary>Total number of items in the batch.</summary>
    public required int TotalItems { get; init; }

    /// <summary>Number of items that converted successfully.</summary>
    public required int SuccessfulConversions { get; init; }

    /// <summary>Number of items that failed conversion.</summary>
    public required int FailedConversions { get; init; }

    /// <summary>Individual conversion results.</summary>
    public required List<FormatConversionResult> Results { get; init; }

    /// <summary>Total elapsed time for the batch.</summary>
    public required TimeSpan Elapsed { get; init; }
}
