namespace InvoiceFlow.Core.Interfaces;

/// <summary>
/// Tenant-aware caching service abstraction.
/// All cache keys are automatically prefixed with the current tenant ID for isolation.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key. Returns null if not found or expired.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a cached value with an optional TTL. Key is automatically tenant-scoped.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a cached value by key.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove all cached values matching a prefix (for cache invalidation patterns).
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a key exists in the cache.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
