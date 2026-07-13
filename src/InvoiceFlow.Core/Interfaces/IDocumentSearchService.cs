using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service interface for full-text search on documents using PostgreSQL tsvector.
/// </summary>
public interface IDocumentSearchService
{
    /// <summary>Maximum page size allowed for search results.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Default page size when none is specified or when an invalid (non-positive) value is provided.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Searches documents using full-text search.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="query">The search query (plain text or websearch syntax).</param>
    /// <param name="documentTypes">Optional filter by document types.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with pagination.</returns>
    Task<DocumentSearchResult> SearchAsync(
        Guid tenantId,
        string query,
        DocumentType[]? documentTypes = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches documents with advanced filters.
    /// Implementations SHOULD clamp <paramref name="pageSize"/> to <see cref="MaxPageSize"/>
    /// and use <see cref="DefaultPageSize"/> when <paramref name="pageSize"/> is less than or equal to zero.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="query">The search query.</param>
    /// <param name="filters">Additional search filters.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with pagination.</returns>
    Task<DocumentSearchResult> SearchAsync(
        Guid tenantId,
        string query,
        DocumentSearchFilters filters,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the search vector for a document (called after OCR extraction or content change).
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateSearchVectorAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds search vectors for all documents in a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RebuildSearchVectorsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search suggestions based on partial query.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="prefix">The query prefix.</param>
    /// <param name="limit">Maximum number of suggestions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(Guid tenantId, string prefix, int limit = 10, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a document search operation.
/// </summary>
public record DocumentSearchResult
{
    /// <summary>The documents matching the search.</summary>
    public required IReadOnlyList<DocumentSearchHit> Hits { get; init; }

    /// <summary>Total number of matching documents.</summary>
    public required long TotalCount { get; init; }

    /// <summary>Current page number.</summary>
    public required int Page { get; init; }

    /// <summary>Number of results per page.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>Time taken for the search in milliseconds.</summary>
    public required long SearchTimeMs { get; init; }
}

/// <summary>
/// A single document search hit.
/// </summary>
public record DocumentSearchHit
{
    /// <summary>Document ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>Document file name.</summary>
    public required string FileName { get; init; }

    /// <summary>Document type.</summary>
    public required DocumentType DocumentType { get; init; }

    /// <summary>Highlighted snippet from the search.</summary>
    public string? Highlight { get; init; }

    /// <summary>Search rank score (higher is more relevant).</summary>
    public required float Rank { get; init; }

    /// <summary>Document creation date.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Linked entity IDs for context.</summary>
    public Guid? LinkedInvoiceId { get; init; }
    public Guid? LinkedCreditNoteId { get; init; }
    public Guid? LinkedDebitNoteId { get; init; }
    public Guid? LinkedPurchaseOrderId { get; init; }
    public Guid? LinkedDeliveryNoteId { get; init; }
    public Guid? LinkedReminderId { get; init; }
}

/// <summary>
/// Advanced search filters.
/// </summary>
public record DocumentSearchFilters
{
    /// <summary>Filter by document types.</summary>
    public DocumentType[]? DocumentTypes { get; init; }

    /// <summary>Filter by date range (document creation).</summary>
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }

    /// <summary>Filter by folder path.</summary>
    public string? Folder { get; init; }

    /// <summary>Filter by tags (JSON array contains any of these).</summary>
    public string[]? Tags { get; init; }

    /// <summary>Filter by linked entity.</summary>
    public Guid? LinkedInvoiceId { get; init; }
    public Guid? LinkedCreditNoteId { get; init; }
    public Guid? LinkedDebitNoteId { get; init; }
    public Guid? LinkedPurchaseOrderId { get; init; }
    public Guid? LinkedDeliveryNoteId { get; init; }
    public Guid? LinkedReminderId { get; init; }

    /// <summary>Only return latest versions.</summary>
    public bool LatestVersionOnly { get; init; } = true;
}