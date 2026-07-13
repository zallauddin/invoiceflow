using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceFlow.Api.Endpoints;

/// <summary>
/// DMS document management endpoints: list, get, search, relationships, and version history.
/// </summary>
public static class DocumentsEndpoints
{
    /// <summary>Input model for creating a document relationship.</summary>
    private sealed record CreateRelationshipInput(
        Guid TargetDocumentId,
        DocumentRelationshipType RelationshipType,
        string? Description);

    public static WebApplication MapDocumentsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        // GET /api/documents — List documents with optional filtering
        group.MapGet("/", async (
            IRepository<Document> repository,
            [FromQuery] string? folder,
            [FromQuery] string? documentType,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken cancellationToken) =>
        {
            var entities = await repository.GetAllAsync(0, 10000, cancellationToken);
            var query = entities.AsQueryable();

            if (!string.IsNullOrEmpty(folder))
                query = query.Where(d => d.Folder == folder);

            if (!string.IsNullOrEmpty(documentType) && Enum.TryParse<DocumentType>(documentType, true, out var docType))
                query = query.Where(d => d.DocumentType == docType);

            var effectivePage = page > 0 ? page : 1;
            var effectivePageSize = pageSize > 0 ? Math.Min(pageSize, 100) : 25;
            var paged = query.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToList();

            return Results.Ok(new
            {
                items = paged,
                page = effectivePage,
                pageSize = effectivePageSize,
                totalCount = query.Count()
            });
        })
        .WithName("ListDocuments")
        .WithSummary("List documents with optional folder and type filtering");

        // GET /api/documents/{id} — Get document by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IRepository<Document> repository,
            CancellationToken cancellationToken) =>
        {
            var entity = await repository.GetByIdAsync(id, cancellationToken);
            return entity is null ? Results.NotFound() : Results.Ok(entity);
        })
        .WithName("GetDocument")
        .WithSummary("Get a document by its ID");

        // GET /api/documents/search — Full-text search across documents
        group.MapGet("/search", async (
            IDocumentSearchService searchService,
            ITenantIdProvider tenantIdProvider,
            [FromQuery] string query,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query))
                return Results.BadRequest("Query parameter is required.");

            var tenantId = tenantIdProvider.TenantId;
            if (tenantId is null || tenantId.Value == Guid.Empty)
                return Results.BadRequest("Tenant not resolved.");

            var effectivePage = page > 0 ? page : 1;
            var effectivePageSize = pageSize > 0 ? Math.Min(pageSize, IDocumentSearchService.MaxPageSize) : IDocumentSearchService.DefaultPageSize;

            var result = await searchService.SearchAsync(
                tenantId.Value,
                query,
                page: effectivePage,
                pageSize: effectivePageSize,
                cancellationToken: cancellationToken);

            return Results.Ok(result);
        })
        .WithName("SearchDocuments")
        .WithSummary("Full-text search across documents using PostgreSQL tsvector");

        // GET /api/documents/{id}/relationships — Get relationships for a document
        group.MapGet("/{id:guid}/relationships", async (
            Guid id,
            IDocumentRelationshipService relationshipService,
            CancellationToken cancellationToken) =>
        {
            var relationships = await relationshipService.GetRelationshipsAsync(id, cancellationToken);
            return Results.Ok(relationships);
        })
        .WithName("GetDocumentRelationships")
        .WithSummary("Get all relationships (incoming and outgoing) for a document");

        // POST /api/documents/{id}/relationships — Create a relationship
        group.MapPost("/{id:guid}/relationships", async (
            Guid id,
            [FromBody] CreateRelationshipInput input,
            IDocumentRelationshipService relationshipService,
            CancellationToken cancellationToken) =>
        {
            var relationship = await relationshipService.CreateRelationshipAsync(
                sourceDocumentId: id,
                targetDocumentId: input.TargetDocumentId,
                relationshipType: input.RelationshipType,
                description: input.Description,
                cancellationToken: cancellationToken);

            return Results.Created($"/api/documents/{id}/relationships", relationship);
        })
        .WithName("CreateDocumentRelationship")
        .WithSummary("Create a relationship between two documents");

        // GET /api/documents/{id}/versions — Get version history for a document
        group.MapGet("/{id:guid}/versions", async (
            Guid id,
            IRepository<DocumentVersionHistory> repository,
            CancellationToken cancellationToken) =>
        {
            var all = await repository.GetAllAsync(0, 10000, cancellationToken);
            var versions = all.Where(v => v.DocumentId == id).OrderByDescending(v => v.Version).ToList();
            return Results.Ok(versions);
        })
        .WithName("GetDocumentVersions")
        .WithSummary("Get version history for a document");

        return app;
    }
}
