namespace Bing.Caching;

/// <summary>
/// 空缓存
/// </summary>
public sealed class NullCache : ILocalCache
{
    /// <summary>
    /// 缓存空实例
    /// </summary>
    public static readonly ILocalCache Instance = new NullCache();

    /// <inheritdoc />
    public bool Exists(CacheKey key) => false;

    /// <inheritdoc />
    public bool Exists(string key) => false;

    /// <inheritdoc />
    public Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public T Get<T>(CacheKey key) => default;

    /// <inheritdoc />
    public T Get<T>(string key) => default;

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null) => dataRetriever == null ? default : dataRetriever();

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null) => dataRetriever == null ? default : dataRetriever();

    /// <inheritdoc />
    public Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default) => null;

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default) => Task.FromResult<T>(default);

    /// <inheritdoc />
    public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T>(default);

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default) =>
        dataRetriever == null ? default : await dataRetriever();

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default) =>
        dataRetriever == null ? default : await dataRetriever();

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys) => [];

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys) => [];

    /// <inheritdoc />
    public Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());

    /// <inheritdoc />
    public Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix) => [];

    /// <inheritdoc />
    public Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default) => Task.FromResult(new List<T>());

    /// <inheritdoc />
    public bool TrySet<T>(CacheKey key, T value, CacheOptions options = null) => false;

    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, CacheOptions options = null) => false;

    /// <inheritdoc />
    public Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public void Set<T>(CacheKey key, T value, CacheOptions options = null)
    {
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, CacheOptions options = null)
    {
    }

    /// <inheritdoc />
    public Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null)
    {
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<string, T> items, CacheOptions options = null)
    {
    }

    /// <inheritdoc />
    public Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Remove(CacheKey key)
    {
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
    }

    /// <inheritdoc />
    public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys)
    {
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
    }

    /// <inheritdoc />
    public Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
    }

    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
    }

    /// <inheritdoc />
    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Clear()
    {
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
