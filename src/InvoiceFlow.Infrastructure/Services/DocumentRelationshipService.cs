using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;
using InvoiceFlow.Core.Interfaces;

namespace InvoiceFlow.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="IDocumentRelationshipService"/>. Document relationship
/// management and graph traversal are not yet implemented — methods throw NotImplementedException.
/// </summary>
public sealed class DocumentRelationshipService : IDocumentRelationshipService
{
    public Task<DocumentRelationship> CreateRelationshipAsync(
        Guid sourceDocumentId,
        Guid targetDocumentId,
        DocumentRelationshipType relationshipType,
        string? description = null,
        string? metadata = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Relationship creation is not yet implemented.");

    public Task<IReadOnlyList<DocumentRelationship>> CreateRelationshipsAsync(
        IEnumerable<CreateDocumentRelationshipRequest> relationships,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Bulk relationship creation is not yet implemented.");

    public Task<IReadOnlyList<DocumentRelationship>> GetRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Relationship retrieval is not yet implemented.");

    public Task<IReadOnlyList<DocumentRelationship>> GetOutgoingRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Outgoing relationship retrieval is not yet implemented.");

    public Task<IReadOnlyList<DocumentRelationship>> GetIncomingRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Incoming relationship retrieval is not yet implemented.");

    public Task<IReadOnlyList<DocumentRelationship>> GetRelationshipsByTypeAsync(
        Guid documentId,
        DocumentRelationshipType relationshipType,
        RelationshipDirection direction = RelationshipDirection.Both,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Typed relationship retrieval is not yet implemented.");

    public Task DeleteRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Relationship deletion is not yet implemented.");

    public Task DeleteAllRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Bulk relationship deletion is not yet implemented.");

    public Task AutoCreateRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Auto-relationship creation is not yet implemented.");

    public Task<DocumentRelationshipGraph> GetRelationshipGraphAsync(Guid documentId, int maxDepth = 3, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Relationship graph traversal is not yet implemented.");
}
