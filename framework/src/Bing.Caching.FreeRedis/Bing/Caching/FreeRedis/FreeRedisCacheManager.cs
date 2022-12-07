using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Extensions;
using Bing.Helpers;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis;

/// <summary>
/// FreeRedis 缓存管理
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class FreeRedisCacheManager : ICache
{
    /// <summary>
    /// Json序列化器
    /// </summary>
    private readonly JsonSerializer _serializer;

    /// <summary>
    /// Redis客户端
    /// </summary>
    private readonly RedisClient _client;

    /// <summary>
    /// 初始化一个<see cref="FreeRedisCacheManager"/>类型的实例
    /// </summary>
    public FreeRedisCacheManager(RedisClient client)
    {
        _client = client;
        _serializer = JsonSerializer.Create();
    }

    /// <summary>
    /// 缓存组件名称
    /// </summary>
    public string Name => CacheType.Redis;

    /// <summary>
    /// 是否存在指定键的缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public bool Exists(string key) => _client.Exists(key);

    /// <summary>
    /// 从缓存中获取数据，如果不存在，则执行获取数据操作并添加到缓存中
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="func">获取数据操作</param>
    /// <param name="expiration">过期时间间隔</param>
    public T Get<T>(string key, Func<T> func, TimeSpan? expiration = null)
    {
        if (_client.Exists(key))
            return _client.Get<T>(key);
        var result = func();
        _client.Set(key, result, GetExpiration(expiration).Seconds);
        return result;
    }

    /// <summary>
    /// 从缓存中获取数据
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    public T Get<T>(string key) => _client.Get<T>(key);

    /// <summary>
    /// 当缓存数据不存在则添加，已存在不会添加，添加成功返回true
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    public bool TryAdd<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            _client.Set(key, value, (int)GetExpiration(expiration).TotalSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 添加缓存。如果已存在缓存，将覆盖
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    public void Add<T>(string key, T value, TimeSpan? expiration = null) => _client.Set(key, value, (int)GetExpiration(expiration).TotalSeconds);

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    public void Remove(string key) => _client.Del(key);

    /// <summary>
    /// 通过缓存键前缀移除缓存
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    public void RemoveByPrefix(string prefix)
    {
        Check.NotNullOrEmpty(prefix, nameof(prefix));
        prefix = this.HandlePrefix(prefix);
        var redisKeys = this.SearchRedisKeys(prefix);
        _client.Del(redisKeys);
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

        return keys.Distinct().ToArray();
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear() => _client.FlushDb();

    /// <summary>
    /// 获取过期时间间隔
    /// </summary>
    /// <param name="expiration">过期时间间隔</param>
    private TimeSpan GetExpiration(TimeSpan? expiration)
    {
        expiration ??= TimeSpan.FromHours(12);
        return expiration.SafeValue();
    }
}