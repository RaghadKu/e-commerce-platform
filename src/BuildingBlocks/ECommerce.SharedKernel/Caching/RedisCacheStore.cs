using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ECommerce.SharedKernel.Caching;

public class RedisCacheStore : ICacheStore
{
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly IDistributedCache _distributedCache;
    private readonly string _globalPrefix;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisCacheStore(IDistributedCache distributedCache, IConnectionMultiplexer redisConnection, string globalPrefix = "EComm_")
    {
        _distributedCache = distributedCache;
        _redisConnection = redisConnection;
        _globalPrefix = globalPrefix;
    }

    public async Task<T?> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        string fullKey = BuildKey(key.Value);
        var cachedBytes = await _distributedCache.GetAsync(fullKey, cancellationToken);

        if (cachedBytes == null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(cachedBytes, SerializerOptions);
    }

    public async Task SetAsync(CacheKey key, object value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        string fullKey = BuildKey(key.Value);
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration.Value;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
        }

        await _distributedCache.SetAsync(fullKey, payloadBytes, options, cancellationToken);
    }

    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        string fullKey = BuildKey(key.Value);
        await _distributedCache.RemoveAsync(fullKey, cancellationToken);
    }

    public async Task<T> GetOrSetAsync<T>(
        CacheKey key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        string fullKey = BuildKey(key.Value);

        // First Check: Fast path without any locking
        var cachedBytes = await _distributedCache.GetAsync(fullKey, cancellationToken);
        if (cachedBytes != null)
        {
            Console.WriteLine($"[Cache] Cache hit for key: {fullKey}");
            return JsonSerializer.Deserialize<T>(cachedBytes, SerializerOptions)!;
        }

        // Get or create a unique semaphore specific to this cache key
        var semaphore = _locks.GetOrAdd(fullKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            // Second Check (Double-Check): Did a waiting thread just populate the cache?
            cachedBytes = await _distributedCache.GetAsync(fullKey, cancellationToken);
            if (cachedBytes != null)
            {
                Console.WriteLine($"[Cache] Cache hit on double-check for key: {fullKey}");
                return JsonSerializer.Deserialize<T>(cachedBytes, SerializerOptions)!;
            }

            // Cache miss verified: Execute the factory to get the fresh value
            var value = await factory();

            // Save the value to Redis cache
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10)
            };

            await _distributedCache.SetAsync(fullKey, payloadBytes, options, cancellationToken);
            return value;
        }
        finally
        {
            semaphore.Release();

            // Optional Cleanup: Remove the semaphore from the dictionary to avoid memory leaks over time
            // (Only safe if no other thread is currently waiting on it)
            if (semaphore.CurrentCount == 1)
            {
                _locks.TryRemove(fullKey, out _);
            }
        }
    }


    public async Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default)
    {
        // Build full pattern, e.g., "EComm_inventory:stock:*"
        string pattern = $"{_globalPrefix}{prefixKey}*";

        var endpoints = _redisConnection.GetEndPoints();
        var server = _redisConnection.GetServer(endpoints.First());
        var db = _redisConnection.GetDatabase();

        // Use SCAN instead of KEYS to avoid blocking the Redis single-threaded loop in production
        var keysToDelete = new List<RedisKey>();
        await Task.Run(() =>
        {
            foreach (var key in server.Keys(pattern: pattern))
            {
                keysToDelete.Add(key);
            }
        }, cancellationToken);

        if (keysToDelete.Count > 0)
        {
            await db.KeyDeleteAsync(keysToDelete.ToArray());
        }
    }

    private string BuildKey(string key) => $"{_globalPrefix}{key}";
}