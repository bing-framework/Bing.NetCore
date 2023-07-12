using System.Collections.Concurrent;
using Bing.Helpers;

namespace Bing.Caching.InMemory;

/// <summary>
/// 内存缓存
/// </summary>
public partial class InMemoryCacheManager : ICache, IDisposable
{
    /// <summary>
    /// 缓存字典
    /// </summary>
    private readonly ConcurrentDictionary<string, CacheItem> _cache;

    /// <summary>
    /// 周期
    /// </summary>
    private int _period = 60;

    /// <summary>
    /// 异步定时器
    /// </summary>
    private Timer _taskTimer;

    /// <summary>
    /// 是否轮询
    /// </summary>
    private bool _polling;

    /// <summary>
    /// 缓存组件名称
    /// </summary>
    public string Name => CacheType.Memory;

    /// <summary>
    /// 初始化一个<see cref="InMemoryCacheManager"/>类型的实例
    /// </summary>
    public InMemoryCacheManager()
    {
        _cache = new ConcurrentDictionary<string, CacheItem>();
        _taskTimer = new Timer(async x => await RemoveNotAliveAsync(x), this, _period * 1000, _period * 1000);
    }

    /// <summary>
    /// 移除已失效的缓存
    /// </summary>
    /// <param name="state">状态</param>
    private Task RemoveNotAliveAsync(object state)
    {
        if (_polling)
            return Task.CompletedTask;
        _polling = true;
        var memoryCacheManager = (InMemoryCacheManager)state;
        var now = DateTime.Now;
        foreach (var item in memoryCacheManager._cache)
        {
            if (item.Value.ExpiredTime < now)
                Remove(item.Key);
        }
        _polling = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 是否存在指定键的缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public bool Exists(string key)
    {
        Check.NotNull(key, nameof(key));
        return _cache.TryGetValue(key, out var item) && item.Expired;
    }

    /// <summary>
    /// 从缓存中获取数据，如果不存在，则执行获取数据操作并添加到缓存中
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="func">获取数据操作</param>
    /// <param name="expiration">过期时间间隔</param>
    public T Get<T>(string key, Func<T> func, TimeSpan? expiration = null)
    {
        Check.NotNull(key, nameof(key));
        if (_cache.TryGetValue(key, out var item) && !item.Expired)
            return (T)item.Visit();
        var value = func.Invoke();
        Add(key, value, expiration);
        return value;
    }

    /// <summary>
    /// 从缓存中获取数据
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    public T Get<T>(string key)
    {
        Check.NotNull(key, nameof(key));
        if (!_cache.TryGetValue(key, out var item) || item.Expired)
            return default;
        return (T)item.Visit();
    }

    /// <summary>
    /// 当缓存数据不存在则添加，已存在不会添加，添加成功返回true
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    public bool TryAdd<T>(string key, T value, TimeSpan? expiration = null)
    {
        Check.NotNull(key, nameof(key));
        if (_cache.TryGetValue(key, out var item) && !item.Expired)
            return false;
        Add(key, value, expiration);
        return true;
    }

    /// <summary>
    /// 添加缓存。如果已存在缓存，将覆盖
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    public void Add<T>(string key, T value, TimeSpan? expiration = null)
    {
        Check.NotNull(key, nameof(key));
        _cache[key] = new CacheItem(value, expiration);
    }

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public void Remove(string key) => _cache.TryRemove(key, out var _);

    /// <summary>
    /// 通过缓存键前缀移除缓存
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    public void RemoveByPrefix(string prefix)
    {
        var keys = _cache.Keys.Where(x => x.StartsWith(prefix));
        foreach (var key in keys)
            Remove(key);
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _cache?.Clear();
        _taskTimer?.Dispose();
        _taskTimer = null;
    }
}
