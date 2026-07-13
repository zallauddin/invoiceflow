using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Supported LLM providers for extraction.
/// </summary>
public enum LlmProvider
{
    /// <summary>Anthropic Claude models.</summary>
    Anthropic,
    
    /// <summary>OpenAI GPT models.</summary>
    OpenAI,
    
    /// <summary>Google Gemini models.</summary>
    Google
}

/// <summary>
/// Options for LLM extraction.
/// </summary>
public record LlmExtractionOptions
{
    /// <summary>
    /// LLM provider to use.
    /// </summary>
    public LlmProvider Provider { get; init; } = LlmProvider.Anthropic;
    
    /// <summary>
    /// Specific model to use (e.g., "claude-3-5-sonnet-20241022", "gpt-4o").
    /// </summary>
    public string Model { get; init; } = string.Empty;
    
    /// <summary>
    /// API key for the provider.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;
    
    /// <summary>
    /// Base URL for the API (optional, for custom endpoints).
    /// </summary>
    public string? BaseUrl { get; init; }
    
    /// <summary>
    /// Maximum tokens for response.
    /// </summary>
    public int MaxTokens { get; init; } = 4096;
    
    /// <summary>
    /// Temperature for generation (0.0 - 1.0).
    /// </summary>
    public double Temperature { get; init; } = 0.1;
    
    /// <summary>
    /// Confidence threshold for extraction.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.85;
}

/// <summary>
/// Result of LLM-based extraction.
/// </summary>
public record LlmExtractionResult
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
    /// Raw response from the LLM.
    /// </summary>
    public string RawResponse { get; init; } = string.Empty;
    
    /// <summary>
    /// Provider used for extraction.
    /// </summary>
    public LlmProvider Provider { get; init; }
    
    /// <summary>
    /// Model used for extraction.
    /// </summary>
    public string Model { get; init; } = string.Empty;
    
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
/// Service for extracting structured invoice data using LLMs as fallback.
/// </summary>
public interface ILlmExtractionService
{
    /// <summary>
    /// Extracts invoice data from text using LLM.
    /// </summary>
    /// <param name="text">Raw text from OCR or document.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<LlmExtractionResult> ExtractFromTextAsync(
        string text,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts invoice data from an image using multimodal LLM.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<LlmExtractionResult> ExtractFromImageAsync(
        string imagePath,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts invoice data from image bytes using multimodal LLM.
    /// </summary>
    /// <param name="imageBytes">Image bytes.</param>
    /// <param name="mimeType">MIME type (e.g., "image/png", "image/jpeg").</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with fields and confidence scores.</returns>
    Task<LlmExtractionResult> ExtractFromImageBytesAsync(
        byte[] imageBytes,
        string mimeType,
        LlmExtractionOptions? options = null,
        CancellationToken cancellationToken = default);
}
