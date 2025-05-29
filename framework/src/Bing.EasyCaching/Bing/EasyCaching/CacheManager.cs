﻿using Bing.Caching;
using Bing.Extensions;

namespace Bing.EasyCaching;

/// <summary>
/// EasyCaching缓存管理
/// </summary>
public class CacheManager : ICache
{
    #region 字段

    /// <summary>
    /// 缓存提供器
    /// </summary>
    private readonly IEasyCachingProviderBase _provider;
    /// <summary>
    /// 缓存提供器
    /// </summary>
    private readonly IEasyCachingProvider _cachingProvider;

    #endregion

    #region 构造方法

    /// <summary>
    /// 初始化EasyCaching缓存服务
    /// </summary>
    /// <param name="provider">EasyCaching缓存提供器</param>
    /// <param name="hybridProvider">EasyCaching 2级缓存提供器</param>
    public CacheManager(IEasyCachingProvider provider, IHybridCachingProvider hybridProvider = null)
    {
        CachingOptions.Clear();
        if (provider != null)
        {
            _provider = provider;
            _cachingProvider = provider;
        }
        if (hybridProvider != null)
            _provider = hybridProvider;
        _provider.CheckNull(nameof(provider));
    }

    #endregion

    #region Exists

    /// <inheritdoc />
    public bool Exists(CacheKey key)
    {
        key.Validate();
        return Exists(key.Key);
    }

    /// <inheritdoc />
    public bool Exists(string key)
    {
        return _provider.Exists(key);
    }

    #endregion

    #region ExistsAsync

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await ExistsAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _provider.ExistsAsync(key, cancellationToken);
    }

    #endregion

    #region Get

    /// <inheritdoc />
    public T Get<T>(CacheKey key)
    {
        key.Validate();
        return Get<T>(key.Key);
    }

    /// <inheritdoc />
    public T Get<T>(string key)
    {
        var result = _provider.Get<T>(key);
        return result.Value;
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys)
    {
        return GetAll<T>(ToKeys(keys));
    }

    /// <summary>
    /// 转换为缓存键字符串集合
    /// </summary>
    private IEnumerable<string> ToKeys(IEnumerable<CacheKey> keys)
    {
        keys.CheckNull(nameof(keys));
        var cacheKeys = keys.ToList();
        cacheKeys.ForEach(t => t.Validate());
        return cacheKeys.Select(t => t.Key);
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys)
    {
        Validate();
        var result = _cachingProvider.GetAll<T>(keys);
        return result.Values.Select(t => t.Value).ToList();
    }

    /// <summary>
    /// 验证
    /// </summary>
    private void Validate()
    {
        if (_cachingProvider == null)
            throw new NotSupportedException("2级缓存不支持该操作");
    }

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> action, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, action, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> action, CacheOptions options = null)
    {
        var result = _provider.Get(key, action, GetExpiration(options));
        return result.Value;
    }

    /// <summary>
    /// 获取过期时间间隔
    /// </summary>
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
        return await _provider.GetAsync(key, type, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync<T>(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var result = await _provider.GetAsync<T>(key, cancellationToken);
        return result.Value;
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default)
    {
        return await GetAllAsync<T>(ToKeys(keys), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        Validate();
        var result = await _cachingProvider.GetAllAsync<T>(keys, cancellationToken);
        return result.Values.Select(t => t.Value).ToList();
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> action, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync(key.Key, action, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> action, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        var result = await _provider.GetAsync(key, action, GetExpiration(options), cancellationToken);
        return result.Value;
    }

    #endregion

    #region GetByPrefix

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
    {
        if (prefix.IsEmpty())
            return new List<T>();
        Validate();
        return _cachingProvider.GetByPrefix<T>(prefix).Where(t => t.Value.HasValue).Select(t => t.Value.Value).ToList();
    }

    #endregion

    #region GetByPrefixAsync

    /// <inheritdoc />
    public async Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default)
    {
        if (prefix.IsEmpty())
            return new List<T>();
        Validate();
        var result = await _cachingProvider.GetByPrefixAsync<T>(prefix, cancellationToken);
        return result.Where(t => t.Value.HasValue).Select(t => t.Value.Value).ToList();
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
    public bool TrySet<T>(string key, T value, CacheOptions options = null)
    {
        return _provider.TrySet(key, value, GetExpiration(options));
    }

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
        return await _provider.TrySetAsync(key, value, GetExpiration(options), cancellationToken);
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
    public void Set<T>(string key, T value, CacheOptions options = null)
    {
        _provider.Set(key, value, GetExpiration(options));
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null)
    {
        SetAll(ToItems(items), options);
    }

    /// <summary>
    /// 转换为缓存项集合
    /// </summary>
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
        _provider.SetAll(items, GetExpiration(options));
    }

    #endregion

    #region SetAsync

    /// <inheritdoc />
    public async Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await SetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        await _provider.SetAsync(key, value, GetExpiration(options), cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        await SetAllAsync(ToItems(items), options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        await _provider.SetAllAsync(items, GetExpiration(options), cancellationToken);
    }

    #endregion

    #region Remove

    /// <inheritdoc />
    public void Remove(CacheKey key)
    {
        key.Validate();
        Remove(key.Key);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        _provider.Remove(key);
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys)
    {
        RemoveAll(ToKeys(keys));
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        _provider.RemoveAll(keys);
    }

    #endregion

    #region RemoveAsync

    /// <inheritdoc />
    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await RemoveAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _provider.RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default)
    {
        await RemoveAllAsync(ToKeys(keys), cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        await _provider.RemoveAllAsync(keys, cancellationToken);
    }

    #endregion

    #region RemoveByPrefix

    /// <summary>
    /// 通过缓存键前缀移除缓存
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    public void RemoveByPrefix(string prefix)
    {
        if (prefix.IsEmpty())
            return;
        _provider.RemoveByPrefix(prefix);
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (prefix.IsEmpty())
            return;
        await _provider.RemoveByPrefixAsync(prefix, cancellationToken);
    }

    #endregion

    #region RemoveByPattern

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        if (pattern.IsEmpty())
            return;
        _provider.RemoveByPattern(pattern);
    }

    #endregion

    #region RemoveByPatternAsync

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (pattern.IsEmpty())
            return;
        await _provider.RemoveByPatternAsync(pattern, cancellationToken);
    }

    #endregion

    #region Clear

    /// <inheritdoc />
    public void Clear()
    {
        Validate();
        _cachingProvider.Flush();
    }

    #endregion

    #region ClearAsync

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Validate();
        await _cachingProvider.FlushAsync(cancellationToken);
    }

    #endregion
}
