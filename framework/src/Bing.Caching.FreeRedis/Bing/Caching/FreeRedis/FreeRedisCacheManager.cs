using Bing.Extensions;
using Bing.Helpers;
using FreeRedis;

namespace Bing.Caching.FreeRedis;

/// <summary>
/// FreeRedis 缓存管理
/// </summary>
// ReSharper disable once InconsistentNaming
public class FreeRedisCacheManager : IRedisCache
{
    /// <summary>
    /// Redis客户端
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个<see cref="FreeRedisCacheManager"/>类型的实例
    /// </summary>
    public FreeRedisCacheManager(RedisClient client)
    {
        if (client.Serialize == null || client.Deserialize == null)
            throw new ArgumentException("FreeRedis 必须设置了序列化/反序列化 client.Serialize/Deserialize");
        _client = client;
    }

    #region Exists

    /// <inheritdoc />
    public bool Exists(CacheKey key)
    {
        key.Validate();
        return Exists(key.Key);
    }

    /// <inheritdoc />
    public bool Exists(string key) => _client.Exists(key);

    #endregion

    #region ExistsAsync

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await ExistsAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => await _client.ExistsAsync(key);

    #endregion

    #region Get

    /// <inheritdoc />
    public T Get<T>(CacheKey key)
    {
        key.Validate();
        return Get<T>(key.Key);
    }

    /// <inheritdoc />
    public T Get<T>(string key) => _client.Get<T>(key);

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, dataRetriever, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null)
    {
        if (_client.Exists(key))
            return _client.Get<T>(key);
        var result = dataRetriever();
        _client.Set(key, result, GetExpiration(options));
        return result;
    }

    /// <summary>
    /// 获取过期时间间隔
    /// </summary>
    /// <param name="options">缓存配置</param>
    private TimeSpan GetExpiration(CacheOptions options)
    {
        var result = options?.Expiration;
        result ??= TimeSpan.FromHours(8);
        return result.SafeValue();
    }

    #endregion

    #region GetAsync

    /// <inheritdoc />
    public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync(key);
        if (result != null)
        {
            var value = _client.Deserialize(result, type);
            return value;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync<T>(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) => await _client.GetAsync<T>(key);

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (await _client.ExistsAsync(key))
            return await _client.GetAsync<T>(key);
        var result = await dataRetriever();
        await _client.SetAsync(key, result, (int)GetExpiration(options).TotalSeconds);
        return result;
    }

    #endregion

    #region GetAll

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys) => GetAll<T>(ToKeys(keys));

    /// <summary>
    /// 转换为缓存键字符串集合
    /// </summary>
    private IEnumerable<string> ToKeys(IEnumerable<CacheKey> keys)
    {
        keys.CheckNull(nameof(keys));
        var cacheKeys = keys.ToList();
        cacheKeys.ForEach(key => key.Validate());
        return cacheKeys.Select(key => key.Key);
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys) => _client.MGet<T>(keys.ToArray()).ToList();

    #endregion

    #region GetAllAsync

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => await GetAllAsync<T>(ToKeys(keys), cancellationToken);

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = await _client.MGetAsync<T>(keys.ToArray());
        return result.ToList();
    }

    #endregion

    #region GetByPrefix

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return new List<T>();
        prefix = this.HandlePrefix(prefix);
        var redisKeys = this.SearchRedisKeys(prefix);

        var result = _client.MGet<T>(redisKeys);

        return result.ToList();
    }

    /// <summary>
    /// 处理缓存键前缀
    /// </summary>
    /// <param name="prefix">前缀</param>
    /// <exception cref="ArgumentException"></exception>
    private string HandlePrefix(string prefix)
    {
        // Forbid
        if (prefix.Equals("*"))
            throw new ArgumentException("the prefix should not equal to *");

        // Don't start with *
        prefix = new System.Text.RegularExpressions.Regex("^\\*+").Replace(prefix, "");

        // End with *
        if (!prefix.EndsWith("*", StringComparison.OrdinalIgnoreCase))
            prefix = string.Concat(prefix, "*");

        //if (!string.IsNullOrWhiteSpace(_client.Nodes?.Values?.FirstOrDefault()?.Prefix))
        //    prefix = _client.Nodes?.Values?.FirstOrDefault()?.Prefix + prefix;

        return prefix;
    }

    /// <summary>
    /// 查询Redis缓存键
    /// </summary>
    /// <param name="pattern">查询模式</param>
    private string[] SearchRedisKeys(string pattern)
    {
        var keys = new List<string>();

        long nextCursor = 0;
        do
        {
            var scanResult = _client.Scan(nextCursor, pattern, 500,"");
            nextCursor = scanResult.cursor;
            var items = scanResult.items;
            keys.AddRange(items);
        }
        while (nextCursor != 0);

        //var prefix = _client.Nodes?.Values?.FirstOrDefault()?.Prefix;

        //if (!string.IsNullOrWhiteSpace(prefix))
        //    keys = keys.Select(x => x.Remove(0, prefix.Length)).ToList();

        return keys.Distinct().ToArray();
    }

    #endregion

    #region GetByPrefixAsync

    /// <inheritdoc />
    public async Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return new List<T>();
        prefix = this.HandlePrefix(prefix);

        var redisKeys = this.SearchRedisKeys(prefix);

        var result = await _client.MGetAsync<T>(redisKeys);

        return result.ToList();
    }

    #endregion

    #region TrySet

    /// <inheritdoc />
    public bool TrySet<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        return TrySet(key.Key, value, options);
    }

    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, CacheOptions options = null) => _client.SetNx(key, value, GetExpiration(options));

    #endregion

    #region TrySetAsync

    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await TrySetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        return await _client.SetNxAsync(key, value, (int)GetExpiration(options).TotalSeconds);
    }

    #endregion

    #region Set

    /// <inheritdoc />
    public void Set<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        Set(key.Key, value, options);
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, CacheOptions options = null) => _client.Set(key, value, GetExpiration(options));

    #endregion

    #region SetAsync

    /// <inheritdoc />
    public async Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await SetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => await _client.SetAsync(key, value, (int)GetExpiration(options).TotalSeconds);

    #endregion

    #region SetAll

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null) => SetAll(ToItems(items), options);

    /// <summary>
    /// 转换为缓存项字典
    /// </summary>
    /// <typeparam name="T">值元素类型</typeparam>
    /// <param name="items">字典</param>
    private IDictionary<string, T> ToItems<T>(IDictionary<CacheKey, T> items)
    {
        items.CheckNull(nameof(items));
        return items.Select(item =>
        {
            item.Key.Validate();
            return new KeyValuePair<string, T>(item.Key.Key, item.Value);
        }).ToDictionary(t => t.Key, t => t.Value);
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<string, T> items, CacheOptions options = null)
    {
        foreach (var item in items)
            _client.Set(item.Key, item.Value, GetExpiration(options));
    }

    #endregion

    #region SetAllAsync

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default) => await SetAllAsync(ToItems(items), options, cancellationToken);

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<bool>>();
        foreach (var item in items)
            tasks.Add(_client.SetNxAsync(item.Key, item.Value, (int)GetExpiration(options).TotalSeconds));
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Remove

    /// <inheritdoc />
    public void Remove(CacheKey key)
    {
        key.Validate();
        Remove(key.Key);
    }

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public void Remove(string key) => _client.Del(key);

    #endregion

    #region RemoveAsync

    /// <inheritdoc />
    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await RemoveAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) => await _client.DelAsync(key);

    #endregion

    #region RemoveAll

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys) => RemoveAll(ToKeys(keys));

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        foreach (var key in keys)
            _client.Del(key);
    }

    #endregion

    #region RemoveAllAsync

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => await RemoveAllAsync(ToKeys(keys), cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<long>>();
        foreach (var key in keys)
            tasks.Add(_client.DelAsync(key));
        await Task.WhenAll(tasks);
    }

    #endregion

    #region RemoveByPrefix

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        Check.NotNullOrWhiteSpace(prefix, nameof(prefix));

        prefix = this.HandlePrefix(prefix);

        var redisKeys = this.SearchRedisKeys(prefix);

        _client.Del(redisKeys);
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(prefix, nameof(prefix));

        prefix = this.HandlePrefix(prefix);

        var redisKeys = this.SearchRedisKeys(prefix);

        await _client.DelAsync(redisKeys);
    }

    #endregion

    #region RemoveByPattern

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        Check.NotNullOrWhiteSpace(pattern, nameof(pattern));

        pattern = this.HandleKeyPattern(pattern);

        var redisKeys = this.SearchRedisKeys(pattern);

        _client.Del(redisKeys);
    }

    /// <summary>
    /// 处理缓存键模式
    /// </summary>
    /// <param name="pattern">缓存键模式</param>
    /// <exception cref="ArgumentException"></exception>
    private string HandleKeyPattern(string pattern)
    {
        if (pattern.Equals("*"))
            throw new ArgumentException("the pattern should not equal to *");

        //if (!string.IsNullOrWhiteSpace(_client.Nodes?.Values?.FirstOrDefault()?.Prefix))
        //    pattern = _client.Nodes?.Values?.FirstOrDefault()?.Prefix + pattern;

        return pattern;
    }

    #endregion

    #region RemoveByPatternAsync

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(pattern, nameof(pattern));

        pattern = this.HandleKeyPattern(pattern);

        var redisKeys = this.SearchRedisKeys(pattern);

        await _client.DelAsync(redisKeys);
    }

    #endregion

    #region Clear

    /// <inheritdoc />
    public void Clear() => _client.FlushDb();

    #endregion

    #region ClearAsync

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _client.FlushDb();
        return Task.CompletedTask;
    }

    #endregion
}
