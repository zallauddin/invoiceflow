using InvoiceFlow.Core.Entities;
using InvoiceFlow.Formats.Abstractions;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// Converts invoice content between two electronic formats using the format registry.
/// The source format is auto-detected from the stream content.
/// Pipeline: detect → read → map → write.
/// </summary>
public class FormatConverter : IFormatConverter
{
    private readonly IFormatRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatConverter"/> class.
    /// </summary>
    /// <param name="registry">The format registry providing readers and writers.</param>
    /// <param name="targetFormat">The target format to convert to.</param>
    public FormatConverter(IFormatRegistry registry, InvoiceFormat targetFormat)
    {
        _registry = registry;
        TargetFormat = targetFormat;
    }

    /// <inheritdoc />
    public InvoiceFormat SourceFormat => InvoiceFormat.Unknown;

    /// <inheritdoc />
    public InvoiceFormat TargetFormat { get; }

    /// <inheritdoc />
    public async Task<FormatConversionResult> ConvertAsync(Stream source, CancellationToken ct = default)
    {
        if (source is null)
        {
            return CreateFailureResult(
                InvoiceFormat.Unknown,
                "Source stream is null.");
        }

        if (!source.CanRead || source.Length == 0)
        {
            return CreateFailureResult(
                InvoiceFormat.Unknown,
                "Source stream is empty or unreadable.");
        }

        // Step 1: Detect format
        source.Position = 0;
        var detectedFormat = _registry.DetectFormat(source);
        source.Position = 0;

        if (detectedFormat == InvoiceFormat.Unknown)
        {
            return CreateFailureResult(
                detectedFormat,
                "Could not detect the source invoice format.");
        }

        // Step 2: Get reader
        var reader = _registry.GetReader(detectedFormat);
        if (reader is null)
        {
            return CreateFailureResult(
                detectedFormat,
                $"No reader registered for source format '{detectedFormat}'.");
        }

        // Step 3: Read source
        FormatReadResult readResult;
        try
        {
            readResult = await reader.ReadAsync(source, ct);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                detectedFormat,
                $"Error reading source format '{detectedFormat}': {ex.Message}");
        }

        // Step 4: Get writer
        var writer = _registry.GetWriter(TargetFormat);
        if (writer is null)
        {
            return new FormatConversionResult
            {
                IsSuccess = false,
                Output = null,
                Invoice = readResult.InvoiceData,
                Lines = readResult.Lines,
                DetectedSourceFormat = detectedFormat,
                SourceValidationResults = readResult.ValidationResults,
                TargetValidationResults = [],
                ErrorMessage = $"No writer registered for target format '{TargetFormat}'.",
            };
        }

        // Step 5: Write target
        FormatWriteResult writeResult;
        try
        {
            writeResult = await writer.WriteAsync(readResult.InvoiceData, readResult.Lines, ct);
        }
        catch (Exception ex)
        {
            return new FormatConversionResult
            {
                IsSuccess = false,
                Output = null,
                Invoice = readResult.InvoiceData,
                Lines = readResult.Lines,
                DetectedSourceFormat = detectedFormat,
                SourceValidationResults = readResult.ValidationResults,
                TargetValidationResults = [],
                ErrorMessage = $"Error writing target format '{TargetFormat}': {ex.Message}",
            };
        }

        // Step 6: Return success
        return new FormatConversionResult
        {
            IsSuccess = true,
            Output = writeResult.Content,
            Invoice = readResult.InvoiceData,
            Lines = readResult.Lines,
            DetectedSourceFormat = detectedFormat,
            SourceValidationResults = readResult.ValidationResults,
            TargetValidationResults = writeResult.ValidationResults,
            ErrorMessage = null,
        };
    }

    private static FormatConversionResult CreateFailureResult(
        InvoiceFormat detectedFormat,
        string errorMessage)
    {
        return new FormatConversionResult
        {
            IsSuccess = false,
            Output = null,
            Invoice = new Invoice(),
            Lines = [],
            DetectedSourceFormat = detectedFormat,
            SourceValidationResults = [],
            TargetValidationResults = [],
            ErrorMessage = errorMessage,
        };
    }
}
