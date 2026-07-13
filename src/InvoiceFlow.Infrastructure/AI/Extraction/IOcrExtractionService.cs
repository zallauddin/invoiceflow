using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Result of OCR extraction from an invoice document.
/// </summary>
public record OcrExtractionResult
{
    /// <summary>
    /// Extracted invoice fields.
    /// </summary>
    public Dictionary<string, string> Fields { get; init; } = new();

    /// <summary>
    /// Confidence score for the overall extraction (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Confidence scores per field.
    /// </summary>
    public Dictionary<string, double> FieldConfidences { get; init; } = new();

    /// <summary>
    /// Raw text extracted from the document.
    /// </summary>
    public string RawText { get; init; } = string.Empty;

    /// <summary>
    /// Language detected/used for extraction.
    /// </summary>
    public string Language { get; init; } = "eng";

    /// <summary>
    /// Processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>
    /// Whether the extraction was successful.
    /// </summary>
    public bool Success => Confidence > 0;
}

/// <summary>
/// Options for OCR extraction.
/// </summary>
public record OcrExtractionOptions
{
    /// <summary>
    /// Language code for Tesseract (e.g., "eng", "deu", "fra", "spa", "ita").
    /// </summary>
    public string Language { get; init; } = "eng";

    /// <summary>
    /// Whether to apply image preprocessing (deskew, contrast, denoise).
    /// </summary>
    public bool PreprocessImage { get; init; } = true;

    /// <summary>
    /// DPI for image rendering (higher = better quality but slower).
    /// </summary>
    public int Dpi { get; init; } = 300;

    /// <summary>
    /// Minimum confidence threshold to consider extraction valid.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.7;
}

/// <summary>
/// Service for extracting structured data from invoice images/documents using OCR.
/// </summary>
public interface IOcrExtractionService
{
    /// <summary>
    /// Extracts invoice data from an image file.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<OcrExtractionResult> ExtractFromImageAsync(
        string imagePath,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts invoice data from a PDF file (first page or all pages).
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<OcrExtractionResult> ExtractFromPdfAsync(
        string pdfPath,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts invoice data from an image stream.
    /// </summary>
    /// <param name="imageStream">Stream containing the image.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<OcrExtractionResult> ExtractFromStreamAsync(
        Stream imageStream,
        OcrExtractionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available languages for OCR.
    /// </summary>
    /// <returns>List of available language codes.</returns>
    Task<IReadOnlyList<string>> GetAvailableLanguagesAsync();
}
