namespace InvoiceFlow.Core.Interfaces;

/// <summary>Generic repository interface for CRUD and pagination operations.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>Gets an entity by its unique identifier.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a paginated list of all entities.</summary>
    Task<IReadOnlyList<T>> GetAllAsync(int skip = 0, int take = 100, CancellationToken cancellationToken = default);

    /// <summary>Adds a new entity and persists it.</summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing entity and persists changes.</summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Deletes an entity and persists the removal.</summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Checks whether an entity with the given ID exists.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the total count of entities (after tenant filtering).</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
