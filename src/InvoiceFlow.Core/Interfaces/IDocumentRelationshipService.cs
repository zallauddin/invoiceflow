using InvoiceFlow.Core.Entities;
using InvoiceFlow.Core.Enums;

namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Service interface for managing document relationships.
/// </summary>
public interface IDocumentRelationshipService
{
    /// <summary>
    /// Creates a relationship between two documents.
    /// </summary>
    /// <param name="sourceDocumentId">The source document ID.</param>
    /// <param name="targetDocumentId">The target document ID.</param>
    /// <param name="relationshipType">The type of relationship.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="createdBy">User who created the relationship.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created relationship.</returns>
    Task<DocumentRelationship> CreateRelationshipAsync(
        Guid sourceDocumentId,
        Guid targetDocumentId,
        DocumentRelationshipType relationshipType,
        string? description = null,
        string? metadata = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates multiple relationships in bulk.
    /// </summary>
    /// <param name="relationships">The relationships to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DocumentRelationship>> CreateRelationshipsAsync(
        IEnumerable<CreateDocumentRelationshipRequest> relationships,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all relationships for a document (both incoming and outgoing).
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DocumentRelationship>> GetRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets outgoing relationships for a document.
    /// </summary>
    /// <param name="documentId">The source document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DocumentRelationship>> GetOutgoingRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets incoming relationships for a document.
    /// </summary>
    /// <param name="documentId">The target document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DocumentRelationship>> GetIncomingRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets relationships of a specific type for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="relationshipType">The relationship type to filter by.</param>
    /// <param name="direction">Direction to search (Outgoing, Incoming, or Both).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<DocumentRelationship>> GetRelationshipsByTypeAsync(
        Guid documentId,
        DocumentRelationshipType relationshipType,
        RelationshipDirection direction = RelationshipDirection.Both,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific relationship.
    /// </summary>
    /// <param name="relationshipId">The relationship ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all relationships for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically creates relationships based on linked entity IDs.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AutoCreateRelationshipsAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the relationship graph for a document (transitive closure up to depth).
    /// Traversal is breadth-first from the root document up to <paramref name="maxDepth"/> levels.
    /// Implementations MUST detect and break cycles: if a node is encountered that has already been
    /// visited, the cycle edge SHOULD still appear in the <see cref="DocumentRelationshipGraph.Edges"/>
    /// collection so the caller can observe the cycle, but the already-visited node MUST NOT be
    /// re-expanded (i.e., it will not appear as a new entry in the <see cref="DocumentRelationshipGraph.Nodes"/>
    /// collection and its outgoing edges will not be traversed again). This prevents infinite loops
    /// in graphs that contain circular references.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="maxDepth">Maximum traversal depth (breadth-first levels from root).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DocumentRelationshipGraph> GetRelationshipGraphAsync(Guid documentId, int maxDepth = 3, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for creating a document relationship.
/// </summary>
public record CreateDocumentRelationshipRequest
{
    /// <summary>The source document ID.</summary>
    public required Guid SourceDocumentId { get; init; }

    /// <summary>The target document ID.</summary>
    public required Guid TargetDocumentId { get; init; }

    /// <summary>The relationship type.</summary>
    public required DocumentRelationshipType RelationshipType { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional metadata.</summary>
    public string? Metadata { get; init; }

    /// <summary>User who created the relationship.</summary>
    public string? CreatedBy { get; init; }
}

/// <summary>
/// Direction for relationship queries.
/// </summary>
public enum RelationshipDirection
{
    /// <summary>Outgoing relationships (source is the document).</summary>
    Outgoing = 0,

    /// <summary>Incoming relationships (target is the document).</summary>
    Incoming = 1,

    /// <summary>Both directions.</summary>
    Both = 2
}

/// <summary>
/// Graph of document relationships.
/// </summary>
public record DocumentRelationshipGraph
{
    /// <summary>The root document ID.</summary>
    public required Guid RootDocumentId { get; init; }

    /// <summary>All nodes in the graph.</summary>
    public required IReadOnlyList<DocumentRelationshipNode> Nodes { get; init; }

    /// <summary>All edges in the graph.</summary>
    public required IReadOnlyList<DocumentRelationshipEdge> Edges { get; init; }
}

/// <summary>
/// Node in a relationship graph.
/// </summary>
public record DocumentRelationshipNode
{
    /// <summary>Document ID.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Document file name.</summary>
    public required string FileName { get; init; }

    /// <summary>Document type.</summary>
    public required DocumentType DocumentType { get; init; }

    /// <summary>Distance from root (0 = root).</summary>
    public required int Depth { get; init; }
}

/// <summary>
/// Edge in a relationship graph.
/// </summary>
public record DocumentRelationshipEdge
{
    /// <summary>Source document ID.</summary>
    public required Guid SourceDocumentId { get; init; }

    /// <summary>Target document ID.</summary>
    public required Guid TargetDocumentId { get; init; }

    /// <summary>Relationship type.</summary>
    public required DocumentRelationshipType RelationshipType { get; init; }
}