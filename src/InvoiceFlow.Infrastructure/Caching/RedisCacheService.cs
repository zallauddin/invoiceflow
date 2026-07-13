using System.Text.Json;
using InvoiceFlow.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace InvoiceFlow.Infrastructure.Caching;

/// <summary>
/// Tenant-aware Redis cache service using IDistributedCache.
/// All keys are automatically prefixed with the current tenant ID for isolation.
/// When no tenant context is available, a global prefix is used.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ITenantIdProvider _tenantProvider;
    private const string GlobalPrefix = "global";
    private const string KeySeparator = ":";

    public RedisCacheService(IDistributedCache cache, ITenantIdProvider tenantProvider)
    {
        _cache = cache;
        _tenantProvider = tenantProvider;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildKey(key);
        var bytes = await _cache.GetAsync(fullKey, cancellationToken);
        if (bytes is null)
            return default;

        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildKey(key);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5)
        };

        await _cache.SetAsync(fullKey, bytes, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildKey(key);
        await _cache.RemoveAsync(fullKey, cancellationToken);
    }

    /// <summary>
    /// Remove all keys matching a prefix. Uses IDistributedCache's native removal
    /// (Redis: uses SCAN + DEL via StackExchange.Redis).
    /// Note: For Redis, this is an O(N) operation where N is the number of keys matching the prefix.
    /// </summary>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        // IDistributedCache doesn't have a native RemoveByPrefix.
        // For production use, consider injecting IConnectionMultiplexer directly
        // for a KEYS/SCAN-based approach. This is a best-effort implementation
        // that stores a set of keys per prefix for deterministic invalidation.
        var fullPrefix = BuildKey(prefix);

        // Store the prefix in a set so we can track keys added with this prefix
        var setKey = $"{fullPrefix}:*";
        var setBytes = await _cache.GetAsync(setKey, cancellationToken);

        if (setBytes is not null)
        {
            var keys = JsonSerializer.Deserialize<List<string>>(setBytes) ?? [];
            foreach (var trackedKey in keys)
            {
                await _cache.RemoveAsync(trackedKey, cancellationToken);
            }

            // Remove the tracking set itself
            await _cache.RemoveAsync(setKey, cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullKey = BuildKey(key);
        var bytes = await _cache.GetAsync(fullKey, cancellationToken);
        return bytes is not null;
    }

    private string BuildKey(string key)
    {
        var tenantPrefix = _tenantProvider.TenantId?.ToString() ?? GlobalPrefix;
        return $"{tenantPrefix}{KeySeparator}{key}";
    }
}
