using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using InvoiceFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// PostgreSQL full-text search implementation of <see cref="IDocumentSearchService"/>.
/// Uses tsvector/tsquery with GIN index for efficient document search.
/// </summary>
public sealed class DocumentSearchService : IDocumentSearchService
{
    private readonly InvoiceFlowDbContext _dbContext;
    private readonly ILogger<DocumentSearchService> _logger;

    public DocumentSearchService(InvoiceFlowDbContext dbContext, ILogger<DocumentSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task UpdateSearchVectorAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE documents 
            SET search_vector = to_tsvector('english', 
                coalesce(file_name, '') || ' ' || 
                coalesce(ocr_text, '') || ' ' || 
                coalesce(tags, ''))
            WHERE id = {0}
            """;

        var rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(sql, documentId, cancellationToken);

        if (rowsAffected == 0)
        {
            _logger.LogWarning("UpdateSearchVectorAsync: No document found with ID {DocumentId}", documentId);
        }
        else
        {
            _logger.LogDebug("UpdateSearchVectorAsync: Updated search vector for document {DocumentId}", documentId);
        }
    }

    /// <inheritdoc />
    public async Task<DocumentSearchResult> SearchAsync(
        Guid tenantId,
        string query,
        DocumentType[]? documentTypes = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filters = new DocumentSearchFilters
        {
            DocumentTypes = documentTypes
        };

        return await SearchAsync(tenantId, query, filters, page, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DocumentSearchResult> SearchAsync(
        Guid tenantId,
        string query,
        DocumentSearchFilters filters,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = IDocumentSearchService.DefaultPageSize;
        if (pageSize > IDocumentSearchService.MaxPageSize) pageSize = IDocumentSearchService.MaxPageSize;

        // Sanitize the query for tsquery: escape special characters and convert to AND terms
        var sanitizedQuery = SanitizeSearchQuery(query);
        var tsQuery = BuildTsQuery(sanitizedQuery);

        if (string.IsNullOrWhiteSpace(tsQuery))
        {
            return new DocumentSearchResult
            {
                Hits = Array.Empty<DocumentSearchHit>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
                SearchTimeMs = sw.ElapsedMilliseconds
            };
        }

        var skip = (page - 1) * pageSize;

        // Build dynamic WHERE clauses based on filters
        var whereClauses = new List<string>
        {
            "d.tenant_id = {0}",
            $"d.search_vector @@ to_tsquery('english', {{1}})",
            "d.is_latest_version = true"
        };

        var parameters = new List<object> { tenantId, tsQuery };
        var paramIndex = 2;

        // Document type filter
        if (filters.DocumentTypes is { Length: > 0 })
        {
            var typeNames = filters.DocumentTypes.Select(t => $"'{t}'");
            whereClauses.Add($"d.document_type IN ({string.Join(", ", typeNames)})");
        }

        // Date range filters
        if (filters.FromDate.HasValue)
        {
            whereClauses.Add($"d.created_at >= {{{paramIndex}}}");
            parameters.Add(filters.FromDate.Value);
            paramIndex++;
        }

        if (filters.ToDate.HasValue)
        {
            whereClauses.Add($"d.created_at <= {{{paramIndex}}}");
            parameters.Add(filters.ToDate.Value);
            paramIndex++;
        }

        // Folder filter
        if (!string.IsNullOrWhiteSpace(filters.Folder))
        {
            whereClauses.Add($"d.folder = {{{paramIndex}}}");
            parameters.Add(filters.Folder);
            paramIndex++;
        }

        // Linked entity filters
        if (filters.LinkedInvoiceId.HasValue)
        {
            whereClauses.Add($"d.linked_invoice_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedInvoiceId.Value);
            paramIndex++;
        }

        if (filters.LinkedCreditNoteId.HasValue)
        {
            whereClauses.Add($"d.linked_credit_note_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedCreditNoteId.Value);
            paramIndex++;
        }

        if (filters.LinkedDebitNoteId.HasValue)
        {
            whereClauses.Add($"d.linked_debit_note_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedDebitNoteId.Value);
            paramIndex++;
        }

        if (filters.LinkedPurchaseOrderId.HasValue)
        {
            whereClauses.Add($"d.linked_purchase_order_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedPurchaseOrderId.Value);
            paramIndex++;
        }

        if (filters.LinkedDeliveryNoteId.HasValue)
        {
            whereClauses.Add($"d.linked_delivery_note_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedDeliveryNoteId.Value);
            paramIndex++;
        }

        if (filters.LinkedReminderId.HasValue)
        {
            whereClauses.Add($"d.linked_reminder_id = {{{paramIndex}}}");
            parameters.Add(filters.LinkedReminderId.Value);
            paramIndex++;
        }

        var whereSql = string.Join(" AND ", whereClauses);

        // Count query
        var countSql = $"SELECT COUNT(*) FROM documents d WHERE {whereSql}";

        // Data query with ts_rank for relevance ordering
        // Note: Cannot use interpolated raw string ($""") here because {{1}} for literal {1}
        // conflicts with interpolation brace parsing. Use non-interpolated raw string instead.
        var dataSql =
            "SELECT d.id, d.file_name, d.document_type, d.created_at," +
            " d.linked_invoice_id, d.linked_credit_note_id, d.linked_debit_note_id," +
            " d.linked_purchase_order_id, d.linked_delivery_note_id, d.linked_reminder_id," +
            " ts_rank(d.search_vector, to_tsquery('english', {1})) AS rank," +
            " ts_headline('english', coalesce(d.ocr_text, ''), to_tsquery('english', {1})," +
            " 'StartSel=<b>, StopSel=</b>, MaxWords=50, MinWords=20') AS highlight" +
            " FROM documents d" +
            $" WHERE {whereSql}" +
            " ORDER BY rank DESC, d.created_at DESC" +
            $" OFFSET {skip} ROWS FETCH NEXT {pageSize} ROWS ONLY";

        var countParameters = parameters.ToArray();
        var countResult = await _dbContext.Database
            .SqlQueryRaw<long>(countSql, countParameters)
            .FirstOrDefaultAsync(cancellationToken);

        var hits = await _dbContext.Database
            .SqlQueryRaw<SearchHitDto>(dataSql, parameters.ToArray())
            .ToListAsync(cancellationToken);

        sw.Stop();

        var result = new DocumentSearchResult
        {
            Hits = hits.Select(h => new DocumentSearchHit
            {
                Id = h.Id,
                FileName = h.FileName,
                DocumentType = h.DocumentType,
                Highlight = h.Highlight,
                Rank = h.Rank,
                CreatedAt = h.CreatedAt,
                LinkedInvoiceId = h.LinkedInvoiceId,
                LinkedCreditNoteId = h.LinkedCreditNoteId,
                LinkedDebitNoteId = h.LinkedDebitNoteId,
                LinkedPurchaseOrderId = h.LinkedPurchaseOrderId,
                LinkedDeliveryNoteId = h.LinkedDeliveryNoteId,
                LinkedReminderId = h.LinkedReminderId
            }).ToList(),
            TotalCount = countResult,
            Page = page,
            PageSize = pageSize,
            SearchTimeMs = sw.ElapsedMilliseconds
        };

        _logger.LogDebug(
            "SearchAsync: query '{Query}' returned {TotalCount} results in {SearchTimeMs}ms",
            query, result.TotalCount, result.SearchTimeMs);

        return result;
    }

    /// <inheritdoc />
    public async Task RebuildSearchVectorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE documents 
            SET search_vector = to_tsvector('english', 
                coalesce(file_name, '') || ' ' || 
                coalesce(ocr_text, '') || ' ' || 
                coalesce(tags, ''))
            WHERE tenant_id = {0}
            """;

        var rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(sql, tenantId, cancellationToken);

        _logger.LogInformation(
            "RebuildSearchVectorsAsync: Rebuilt search vectors for {RowsAffected} documents in tenant {TenantId}",
            rowsAffected, tenantId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        Guid tenantId,
        string prefix,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Array.Empty<string>();
        }

        if (limit <= 0) limit = 10;

        const string sql = """
            SELECT DISTINCT file_name 
            FROM documents 
            WHERE tenant_id = {0} 
              AND file_name ILIKE {1}
            ORDER BY file_name
            LIMIT {2}
            """;

        var pattern = $"{EscapeLikePattern(prefix)}%";
        var suggestions = await _dbContext.Database
            .SqlQueryRaw<string>(sql, tenantId, pattern, limit)
            .ToListAsync(cancellationToken);

        return suggestions;
    }

    /// <summary>
    /// Sanitizes a search query string by removing special tsquery characters.
    /// </summary>
    private static string SanitizeSearchQuery(string query)
    {
        // Remove characters that are invalid in tsquery
        var sanitized = System.Text.RegularExpressions.Regex.Replace(query, @"[^\w\s\-]", " ");
        return sanitized.Trim();
    }

    /// <summary>
    /// Builds a PostgreSQL tsquery string from sanitized input words.
    /// Each word is AND-ed together using the &amp; operator.
    /// </summary>
    private static string BuildTsQuery(string sanitizedQuery)
    {
        if (string.IsNullOrWhiteSpace(sanitizedQuery))
            return string.Empty;

        var words = sanitizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
            return string.Empty;

        // Escape each word for tsquery safety (append :* for prefix matching)
        var terms = words.Select(w => EscapeTsQueryWord(w) + ":*");
        return string.Join(" & ", terms);
    }

    /// <summary>
    /// Escapes a single word for safe use in a tsquery operand.
    /// </summary>
    private static string EscapeTsQueryWord(string word)
    {
        // tsquery operands must not contain special characters
        return System.Text.RegularExpressions.Regex.Replace(word, @"[^\w]", "");
    }

    /// <summary>
    /// Escapes special characters for use in PostgreSQL LIKE pattern.
    /// </summary>
    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    /// <summary>
    /// DTO for mapping search results from raw SQL.
    /// </summary>
    private sealed class SearchHitDto
    {
        public Guid Id { get; set; } = default;
        public string FileName { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; } = default;
        public DateTime CreatedAt { get; set; } = default;
        public Guid? LinkedInvoiceId { get; set; }
        public Guid? LinkedCreditNoteId { get; set; }
        public Guid? LinkedDebitNoteId { get; set; }
        public Guid? LinkedPurchaseOrderId { get; set; }
        public Guid? LinkedDeliveryNoteId { get; set; }
        public Guid? LinkedReminderId { get; set; }
        public float Rank { get; set; }
        public string? Highlight { get; set; }
    }
}
