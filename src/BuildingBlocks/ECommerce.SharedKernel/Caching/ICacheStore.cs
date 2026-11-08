namespace ECommerce.SharedKernel.Caching;

public interface ICacheStore
{
    Task<T?> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default);
    Task SetAsync(CacheKey key, object value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    Task<T> GetOrSetAsync<T>(
        CacheKey key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default);
}

public record CacheKey(string Value);