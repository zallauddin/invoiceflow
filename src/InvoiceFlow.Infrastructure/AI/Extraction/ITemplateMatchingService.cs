using InvoiceFlow.Core.Entities;

namespace InvoiceFlow.Infrastructure.AI.Extraction;

/// <summary>
/// Represents a learned invoice template pattern.
/// </summary>
public record InvoiceTemplate
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Human-readable name for the template.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Description of the template/layout type.
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Key visual/layout characteristics for matching.
    /// </summary>
    public Dictionary<string, string> VisualFeatures { get; init; } = new();
    
    /// <summary>
    /// Field mappings for this template (field name -> extraction strategy).
    /// </summary>
    public Dictionary<string, FieldMapping> FieldMappings { get; init; } = new();
    
    /// <summary>
    /// Example raw text from this template (for few-shot learning).
    /// </summary>
    public string ExampleText { get; init; } = string.Empty;
    
    /// <summary>
    /// Confidence threshold for this template.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.85;
    
    /// <summary>
    /// Number of successful extractions using this template.
    /// </summary>
    public int UsageCount { get; init; } = 0;
    
    /// <summary>
    /// Average extraction accuracy for this template.
    /// </summary>
    public double AverageAccuracy { get; init; } = 0.0;
    
    /// <summary>
    /// When this template was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this template was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Mapping strategy for a specific field in a template.
/// </summary>
public record FieldMapping
{
    /// <summary>
    /// Field name in the internal model.
    /// </summary>
    public string FieldName { get; init; } = string.Empty;
    
    /// <summary>
    /// LLM prompt fragment for extracting this field.
    /// </summary>
    public string ExtractionPrompt { get; init; } = string.Empty;
    
    /// <summary>
    /// Regex pattern for this field (if applicable).
    /// </summary>
    public string? RegexPattern { get; init; }
    
    /// <summary>
    /// Whether this field is required for this template.
    /// </summary>
    public bool IsRequired { get; init; } = false;
    
    /// <summary>
    /// Expected data type.
    /// </summary>
    public string DataType { get; init; } = "string";
}

/// <summary>
/// Result of template matching.
/// </summary>
public record TemplateMatchResult
{
    /// <summary>
    /// Whether a matching template was found.
    /// </summary>
    public bool HasMatch { get; init; }
    
    /// <summary>
    /// The matched template (if any).
    /// </summary>
    public InvoiceTemplate? Template { get; init; }
    
    /// <summary>
    /// Confidence score for the match (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; init; }
    
    /// <summary>
    /// Extracted fields using the template.
    /// </summary>
    public Dictionary<string, string> ExtractedFields { get; init; } = new();
    
    /// <summary>
    /// Field confidences.
    /// </summary>
    public Dictionary<string, double> FieldConfidences { get; init; } = new();
}

/// <summary>
/// Service for AI-powered template matching and self-learning extraction.
/// </summary>
public interface ITemplateMatchingService
{
    /// <summary>
    /// Matches an invoice document to a known template or creates a new one.
    /// </summary>
    /// <param name="rawText">Raw text from the document.</param>
    /// <param name="imagePath">Optional path to the image for visual analysis.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Template match result with extracted fields.</returns>
    Task<TemplateMatchResult> MatchOrCreateTemplateAsync(
        string rawText,
        string? imagePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all learned templates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of learned templates.</returns>
    Task<IReadOnlyList<InvoiceTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a template with new extraction results for learning.
    /// </summary>
    /// <param name="templateId">Template ID to update.</param>
    /// <param name="extractedFields">Fields extracted from a new document.</param>
    /// <param name="accuracy">Accuracy of the extraction (0.0 - 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated template.</returns>
    Task<InvoiceTemplate> UpdateTemplateAsync(
        Guid templateId,
        Dictionary<string, string> extractedFields,
        double accuracy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template from a successful extraction.
    /// </summary>
    /// <param name="name">Template name.</param>
    /// <param name="rawText">Raw text from the document.</param>
    /// <param name="extractedFields">Fields extracted from the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Newly created template.</returns>
    Task<InvoiceTemplate> CreateTemplateAsync(
        string name,
        string rawText,
        Dictionary<string, string> extractedFields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template.
    /// </summary>
    /// <param name="templateId">Template ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
}
