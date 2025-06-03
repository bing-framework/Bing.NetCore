using System.Text;
using Bing.Extensions;
using Bing.Helpers;
using CSRedis;
using Newtonsoft.Json;

namespace Bing.Caching.CSRedis;

/// <summary>
/// CSRedis 缓存管理
/// </summary>
// ReSharper disable once InconsistentNaming
public class CSRedisCacheManager : IRedisCache
{
    /// <summary>
    /// Json序列化器
    /// </summary>
    private readonly JsonSerializer _serializer;

    /// <summary>
    /// 初始化一个<see cref="CSRedisCacheManager"/>类型的实例
    /// </summary>
    public CSRedisCacheManager() => _serializer = JsonSerializer.Create();

    #region Exists

    /// <inheritdoc />
    public bool Exists(CacheKey key)
    {
        key.Validate();
        return Exists(key.Key);
    }

    /// <inheritdoc />
    public bool Exists(string key) => RedisHelper.Exists(key);

    #endregion

    #region ExistsAsync

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await ExistsAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => await RedisHelper.ExistsAsync(key);

    #endregion

    #region Get

    /// <inheritdoc />
    public T Get<T>(CacheKey key)
    {
        key.Validate();
        return Get<T>(key.Key);
    }

    /// <inheritdoc />
    public T Get<T>(string key) => RedisHelper.Get<T>(key);

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, dataRetriever, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null)
    {
        if (RedisHelper.Exists(key))
            return RedisHelper.Get<T>(key);
        var result = dataRetriever();
        RedisHelper.Set(key, result, GetExpiration(options));
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
        var result = await RedisHelper.GetAsync<byte[]>(key);
        if (result != null)
        {
            var value = Deserialize(result, type);
            return value;
        }
        return null;
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <param name="type">类型</param>
    internal object Deserialize(byte[] bytes, Type type)
    {
        using var ms = new MemoryStream(bytes);
        using var sr = new StreamReader(ms, Encoding.UTF8);
        using var jtr = new JsonTextReader(sr);
        return _serializer.Deserialize(jtr, type);
    }


    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync<T>(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) => await RedisHelper.GetAsync<T>(key);

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (await RedisHelper.ExistsAsync(key))
            return await RedisHelper.GetAsync<T>(key);
        var result = await dataRetriever();
        await RedisHelper.SetAsync(key, result, GetExpiration(options));
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
    public List<T> GetAll<T>(IEnumerable<string> keys) => RedisHelper.MGet<T>(keys.ToArray()).ToList();

    #endregion

    #region GetAllAsync

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => await GetAllAsync<T>(ToKeys(keys), cancellationToken);

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var result = await RedisHelper.MGetAsync<T>(keys.ToArray());
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

        var result = RedisHelper.MGet<T>(redisKeys);

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

        if (!string.IsNullOrWhiteSpace(RedisHelper.Nodes?.Values?.FirstOrDefault()?.Prefix))
            prefix = RedisHelper.Nodes?.Values?.FirstOrDefault()?.Prefix + prefix;

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
            var scanResult = RedisHelper.Scan(nextCursor, pattern, 500);
            nextCursor = scanResult.Cursor;
            var items = scanResult.Items;
            keys.AddRange(items);
        }
        while (nextCursor != 0);

        var prefix = RedisHelper.Nodes?.Values?.FirstOrDefault()?.Prefix;

        if (!string.IsNullOrWhiteSpace(prefix))
            keys = keys.Select(x => x.Remove(0, prefix.Length)).ToList();

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

        var result = await RedisHelper.MGetAsync<T>(redisKeys);

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
    public bool TrySet<T>(string key, T value, CacheOptions options = null) => RedisHelper.Set(key, value, GetExpiration(options), RedisExistence.Nx);

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
        return await RedisHelper.SetAsync(key, value, GetExpiration(options), RedisExistence.Nx);
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
    public void Set<T>(string key, T value, CacheOptions options = null) => RedisHelper.Set(key, value, GetExpiration(options));

    #endregion

    #region SetAsync

    /// <inheritdoc />
    public async Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await SetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default) => await RedisHelper.SetAsync(key, value, GetExpiration(options));

    #endregion

    #region SetAll

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null) => SetAll(ToItems(items),options);

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
            RedisHelper.Set(item.Key, item.Value, GetExpiration(options));
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
            tasks.Add(RedisHelper.SetAsync(item.Key, item.Value, GetExpiration(options)));
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
    public void Remove(string key) => RedisHelper.Del(key);

    #endregion

    #region RemoveAsync

    /// <inheritdoc />
    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await RemoveAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) => await RedisHelper.DelAsync(key);

    #endregion

    #region RemoveAll

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys) => RemoveAll(ToKeys(keys));

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        foreach (var key in keys) 
            RedisHelper.Del(key);
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
            tasks.Add(RedisHelper.DelAsync(key));
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

        RedisHelper.Del(redisKeys);
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrWhiteSpace(prefix, nameof(prefix));

        prefix = this.HandlePrefix(prefix);

        var redisKeys = this.SearchRedisKeys(prefix);

        await RedisHelper.DelAsync(redisKeys);
    }

    #endregion

    #region RemoveByPattern

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        Check.NotNullOrWhiteSpace(pattern, nameof(pattern));

        pattern = this.HandleKeyPattern(pattern);

        var redisKeys = this.SearchRedisKeys(pattern);

        RedisHelper.Del(redisKeys);
    }

    /// <summary>
    /// 处理缓存键模式
    /// </summary>
    /// <param name="pattern">缓存键模式</param>
    /// <exception cref="ArgumentException"></exception>
    private string HandleKeyPattern(string pattern)
    {
        if(pattern.Equals("*"))
            throw new ArgumentException("the pattern should not equal to *");

        if (!string.IsNullOrWhiteSpace(RedisHelper.Nodes?.Values?.FirstOrDefault()?.Prefix))
            pattern = RedisHelper.Nodes?.Values?.FirstOrDefault()?.Prefix + pattern;

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

        await RedisHelper.DelAsync(redisKeys);
    }

    #endregion

    #region Clear

    /// <inheritdoc />
    public void Clear() => RedisHelper.NodesServerManager.FlushDb();

    #endregion

    #region ClearAsync

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default) => await RedisHelper.NodesServerManager.FlushDbAsync();

    #endregion
}
