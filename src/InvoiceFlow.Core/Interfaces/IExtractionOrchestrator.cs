using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Events;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Result of the extraction orchestration pipeline.
/// </summary>
public record ExtractionOrchestratorResult
{
    /// <summary>Whether the extraction completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>The extraction method that produced the final result.</summary>
    public ExtractionMethod ExtractionMethod { get; init; }

    /// <summary>Overall confidence score for the extraction (0.0 - 1.0).</summary>
    public double Confidence { get; init; }

    /// <summary>The populated invoice with extracted data.</summary>
    public Invoice Invoice { get; init; } = null!;

    /// <summary>Domain events raised during extraction.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents { get; init; } = Array.Empty<IDomainEvent>();

    /// <summary>Error message if the extraction failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Total processing time in milliseconds.</summary>
    public long ProcessingTimeMs { get; init; }

    /// <summary>Creates a failed result with the given error message.</summary>
    public static ExtractionOrchestratorResult Fail(string errorMessage, long processingTimeMs = 0) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ProcessingTimeMs = processingTimeMs
    };
}

/// <summary>
/// Orchestrates the multi-stage invoice data extraction pipeline:
/// OCR → LLM fallback → Template matching.
/// </summary>
public interface IExtractionOrchestrator
{
    /// <summary>
    /// Runs the full extraction pipeline against a document, populating the invoice
    /// with extracted fields, line items, and totals.
    /// </summary>
    /// <param name="document">The source document containing the invoice image/PDF.</param>
    /// <param name="invoice">The invoice entity to populate with extracted data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the populated invoice, extraction metadata, and domain events.</returns>
    Task<ExtractionOrchestratorResult> ProcessAsync(
        Document document,
        Invoice invoice,
        CancellationToken cancellationToken = default);
}
