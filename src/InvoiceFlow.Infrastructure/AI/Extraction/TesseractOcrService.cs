using System.Text.RegularExpressions;
using Tesseract;
using InvoiceFlow.Infrastructure.AI.Extraction;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Tesseract.NET implementation of OCR extraction service.
/// </summary>
public sealed class TesseractOcrService : IOcrExtractionService, IDisposable
{
    private readonly string _tessDataPath;
    private readonly Dictionary<string, Regex> _fieldPatterns;
    private bool _disposed;

    public TesseractOcrService(string? tessDataPath = null)
    {
        _tessDataPath = tessDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
        
        if (!Directory.Exists(_tessDataPath))
        {
            // Try common locations
            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR", "tessdata"),
                "/usr/share/tesseract-ocr/4.00/tessdata",
                "/usr/share/tessdata",
            };
            
            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    _tessDataPath = path;
                    break;
                }
            }
        }

        _fieldPatterns = InitializeFieldPatterns();
    }

    /// <inheritdoc />
    public async Task<OcrExtractionResult> ExtractFromImageAsync(
        string imagePath,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OcrExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var image = Pix.LoadFromFile(imagePath);
            var result = await ExtractFromPixAsync(image, options, cancellationToken);
            return result with { ProcessingTimeMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            return new OcrExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawText = $"Error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<OcrExtractionResult> ExtractFromPdfAsync(
        string pdfPath,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OcrExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // For PDF, we need to render pages to images first
            // This requires a PDF rendering library (PDFium, etc.)
            // For now, throw not supported - implement when PDF library is added
            throw new NotSupportedException("PDF extraction requires PDFium or similar library. Use ExtractFromImageAsync for now.");
        }
        catch (Exception ex)
        {
            return new OcrExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawText = $"Error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<OcrExtractionResult> ExtractFromStreamAsync(
        Stream imageStream,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new OcrExtractionOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Save stream to temp file for Tesseract
            var tempPath = Path.GetTempFileName() + ".png";
            await using (var fileStream = File.Create(tempPath))
            {
                await imageStream.CopyToAsync(fileStream, cancellationToken);
            }

            try
            {
                var result = await ExtractFromImageAsync(tempPath, options, cancellationToken);
                return result with { ProcessingTimeMs = stopwatch.ElapsedMilliseconds };
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            return new OcrExtractionResult
            {
                Confidence = 0,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                RawText = $"Error: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAvailableLanguagesAsync()
    {
        try
        {
            if (!Directory.Exists(_tessDataPath))
                return Task.FromResult<IReadOnlyList<string>>(new[] { "eng" });

            var languages = Directory.GetFiles(_tessDataPath, "*.traineddata")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Where(l => !l.StartsWith("."))
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(languages.Count > 0 ? languages : new[] { "eng" });
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<string>>(new[] { "eng" });
        }
    }

    private async Task<OcrExtractionResult> ExtractFromPixAsync(
        Pix image,
        OcrExtractionOptions options,
        CancellationToken cancellationToken)
    {
        using var engine = new TesseractEngine(_tessDataPath, options.Language, EngineMode.Default);
        
        // Configure engine
        engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;/()-€$£¥% ");
        engine.SetVariable("preserve_interword_spaces", "1");

        Pix processedImage = image;
        if (options.PreprocessImage)
        {
            processedImage = PreprocessImage(image);
        }

        using var page = engine.Process(processedImage, PageSegMode.Auto);
        var rawText = page.GetText();
        var confidence = page.GetMeanConfidence();

        var fields = ExtractFields(rawText);
        var fieldConfidences = CalculateFieldConfidences(fields, rawText, confidence);

        var overallConfidence = fieldConfidences.Values.Any() 
            ? fieldConfidences.Values.Average() 
            : confidence / 100.0;

        return new OcrExtractionResult
        {
            Fields = fields,
            Confidence = overallConfidence,
            FieldConfidences = fieldConfidences,
            RawText = rawText,
            Language = options.Language,
        };
    }

    private Pix PreprocessImage(Pix image)
    {
        // Deskew
        var deskewed = image.Deskew();
        return deskewed;
    }

    private Dictionary<string, string> ExtractFields(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var (fieldName, pattern) in _fieldPatterns)
        {
            foreach (var line in lines)
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    var value = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && !fields.ContainsKey(fieldName))
                    {
                        fields[fieldName] = value;
                        break;
                    }
                }
            }
        }

        // Also try to find amount totals
        ExtractAmounts(text, fields);
        
        return fields;
    }

    private void ExtractAmounts(string text, Dictionary<string, string> fields)
    {
        // Look for total amount patterns
        var totalPatterns = new[]
        {
            @"(?:total|amount due|grand total|sum)\s*[:=]?\s*([€$£¥]?\s*\d+[.,]\d{2})",
            @"(?:total|amount)\s*[:=]?\s*([€$£¥]?\s*\d+[.,]\d{2})",
        };

        foreach (var pattern in totalPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && !fields.ContainsKey("TotalAmount"))
            {
                fields["TotalAmount"] = match.Groups[1].Value.Trim();
                break;
            }
        }

        // Look for tax amount
        var taxPatterns = new[]
        {
            @"(?:tax|vat|mwst|btw|iva)\s*[:=]?\s*([€$£¥]?\s*\d+[.,]\d{2})",
        };

        foreach (var pattern in taxPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && !fields.ContainsKey("TaxAmount"))
            {
                fields["TaxAmount"] = match.Groups[1].Value.Trim();
                break;
            }
        }
    }

    private Dictionary<string, double> CalculateFieldConfidences(
        Dictionary<string, string> fields, 
        string rawText, 
        float overallConfidence)
    {
        var confidences = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var baseConfidence = overallConfidence / 100.0;

        foreach (var field in fields)
        {
            // Adjust confidence based on field type and pattern match quality
            var fieldConfidence = baseConfidence;
            
            // Certain fields are more reliably extracted
            if (field.Key is "InvoiceNumber" or "InvoiceDate" or "TotalAmount")
            {
                fieldConfidence = Math.Min(1.0, baseConfidence * 1.1);
            }
            
            confidences[field.Key] = fieldConfidence;
        }

        return confidences;
    }

    private OcrExtractionResult CombineResults(List<OcrExtractionResult> results)
    {
        if (results.Count == 0)
            return new OcrExtractionResult { Confidence = 0 };

        if (results.Count == 1)
            return results[0];

        var combined = new OcrExtractionResult
        {
            Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            FieldConfidences = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            RawText = string.Join("\n\n--- PAGE BREAK ---\n\n", results.Select(r => r.RawText)),
            Language = results[0].Language,
            Confidence = results.Average(r => r.Confidence),
        };

        // Merge fields, preferring higher confidence
        foreach (var result in results)
        {
            foreach (var field in result.Fields)
            {
                var existingConfidence = combined.FieldConfidences.GetValueOrDefault(field.Key, 0);
                var newConfidence = result.FieldConfidences.GetValueOrDefault(field.Key, 0);
                
                if (newConfidence > existingConfidence)
                {
                    combined.Fields[field.Key] = field.Value;
                    combined.FieldConfidences[field.Key] = newConfidence;
                }
            }
        }

        return combined;
    }

    private Dictionary<string, Regex> InitializeFieldPatterns()
    {
        return new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = new Regex(@"(?:invoice|inv|rechnung|facture|fattura)\s*(?:no|number|#|nr)\.?\s*[:=]?\s*([A-Z0-9\-/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["InvoiceDate"] = new Regex(@"(?:invoice|date|datum|fecha|data)\s*(?:date)?\.?\s*[:=]?\s*(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["DueDate"] = new Regex(@"(?:due|pay by|fälligkeit|vencimiento|scadenza)\s*(?:date)?\.?\s*[:=]?\s*(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["VendorName"] = new Regex(@"(?:from|vendor|seller|supplier|lieferant|fournisseur|cedente)\s*[:=]?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["VendorTaxId"] = new Regex(@"(?:tax id|vat|ust-id|nif|nip|piva|siret)\s*[:=]?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["CustomerName"] = new Regex(@"(?:to|bill to|customer|buyer|kunde|client|cessionario)\s*[:=]?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["CustomerTaxId"] = new Regex(@"(?:customer|buyer)\s*(?:tax id|vat|ust-id|nif|nip|piva)\s*[:=]?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["Currency"] = new Regex(@"(?:currency|währung|devise|valuta)\s*[:=]?\s*([A-Z]{3})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["IBAN"] = new Regex(@"(?:iban)\s*[:=]?\s*([A-Z]{2}\d{2}[A-Z0-9]{11,30})", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            ["BIC"] = new Regex(@"(?:bic|swift)\s*[:=]?\s*([A-Z]{6}[A-Z0-9]{2}(?:[A-Z0-9]{3})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}