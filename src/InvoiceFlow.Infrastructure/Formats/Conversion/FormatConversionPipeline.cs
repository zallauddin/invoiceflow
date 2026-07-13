using System.Diagnostics;
using InvoiceFlow.Formats.Abstractions;

namespace InvoiceFlow.Infrastructure.Formats.Conversion;

/// <summary>
/// High-level pipeline for converting invoice formats, batch processing, and format detection.
/// Creates per-conversion <see cref="FormatConverter"/> instances using the shared format registry.
/// </summary>
public class FormatConversionPipeline : IFormatConversionPipeline
{
    private readonly IFormatRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatConversionPipeline"/> class.
    /// </summary>
    /// <param name="registry">The format registry providing readers, writers, and descriptors.</param>
    public FormatConversionPipeline(IFormatRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<FormatConversionResult> ConvertAsync(
        Stream source,
        InvoiceFormat targetFormat,
        CancellationToken ct = default)
    {
        var converter = new FormatConverter(_registry, targetFormat);
        return await converter.ConvertAsync(source, ct);
    }

    /// <inheritdoc />
    public async Task<FormatConversionResult> ConvertAsync(
        Stream source,
        InvoiceFormat sourceFormat,
        InvoiceFormat targetFormat,
        CancellationToken ct = default)
    {
        if (sourceFormat == InvoiceFormat.Unknown)
        {
            return await ConvertAsync(source, targetFormat, ct);
        }

        // Validate source format is readable
        var reader = _registry.GetReader(sourceFormat);
        if (reader is null)
        {
            return new FormatConversionResult
            {
                IsSuccess = false,
                Output = null,
                Invoice = new Core.Entities.Invoice(),
                Lines = [],
                DetectedSourceFormat = sourceFormat,
                SourceValidationResults = [],
                TargetValidationResults = [],
                ErrorMessage = $"No reader registered for source format '{sourceFormat}'.",
            };
        }

        // Validate target format is writable
        var writer = _registry.GetWriter(targetFormat);
        if (writer is null)
        {
            return new FormatConversionResult
            {
                IsSuccess = false,
                Output = null,
                Invoice = new Core.Entities.Invoice(),
                Lines = [],
                DetectedSourceFormat = sourceFormat,
                SourceValidationResults = [],
                TargetValidationResults = [],
                ErrorMessage = $"No writer registered for target format '{targetFormat}'.",
            };
        }

        // Read from the known source
        FormatReadResult readResult;
        try
        {
            source.Position = 0;
            readResult = await reader.ReadAsync(source, ct);
        }
        catch (Exception ex)
        {
            return new FormatConversionResult
            {
                IsSuccess = false,
                Output = null,
                Invoice = new Core.Entities.Invoice(),
                Lines = [],
                DetectedSourceFormat = sourceFormat,
                SourceValidationResults = [],
                TargetValidationResults = [],
                ErrorMessage = $"Error reading source format '{sourceFormat}': {ex.Message}",
            };
        }

        // Write to target
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
                DetectedSourceFormat = sourceFormat,
                SourceValidationResults = readResult.ValidationResults,
                TargetValidationResults = [],
                ErrorMessage = $"Error writing target format '{targetFormat}': {ex.Message}",
            };
        }

        return new FormatConversionResult
        {
            IsSuccess = true,
            Output = writeResult.Content,
            Invoice = readResult.InvoiceData,
            Lines = readResult.Lines,
            DetectedSourceFormat = sourceFormat,
            SourceValidationResults = readResult.ValidationResults,
            TargetValidationResults = writeResult.ValidationResults,
            ErrorMessage = null,
        };
    }

    /// <inheritdoc />
    public async Task<BatchConversionResult> ConvertBatchAsync(
        BatchConversionRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<FormatConversionResult>(request.Items.Count);
        var successCount = 0;
        var failCount = 0;

        foreach (var item in request.Items)
        {
            ct.ThrowIfCancellationRequested();

            FormatConversionResult result;

            if (item.KnownSourceFormat.HasValue && item.KnownSourceFormat.Value != InvoiceFormat.Unknown)
            {
                result = await ConvertAsync(
                    item.Content,
                    item.KnownSourceFormat.Value,
                    request.TargetFormat,
                    ct);
            }
            else
            {
                result = await ConvertAsync(item.Content, request.TargetFormat, ct);
            }

            results.Add(result);

            if (result.IsSuccess)
            {
                successCount++;
            }
            else
            {
                failCount++;
                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();

        return new BatchConversionResult
        {
            TotalItems = request.Items.Count,
            SuccessfulConversions = successCount,
            FailedConversions = failCount,
            Results = results,
            Elapsed = stopwatch.Elapsed,
        };
    }

    /// <inheritdoc />
    public Task<FormatDetectionResult> DetectFormatAsync(
        Stream content,
        string? fileName = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (content is null || !content.CanRead || content.Length == 0)
        {
            return Task.FromResult(new FormatDetectionResult
            {
                DetectedFormat = InvoiceFormat.Unknown,
                Descriptor = null,
                Confidence = 0.0,
            });
        }

        var detectedFormat = _registry.DetectFormat(content, fileName);
        var descriptor = _registry.GetDescriptor(detectedFormat);

        // Confidence heuristic:
        //   Known format + registered descriptor → 1.0
        //   Known format + no descriptor           → 0.7
        //   Unknown format                         → 0.0
        var confidence = detectedFormat switch
        {
            InvoiceFormat.Unknown => 0.0,
            _ when descriptor is not null => 1.0,
            _ => 0.7,
        };

        return Task.FromResult(new FormatDetectionResult
        {
            DetectedFormat = detectedFormat,
            Descriptor = descriptor,
            Confidence = confidence,
        });
    }
}
