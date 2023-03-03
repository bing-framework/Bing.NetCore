using System.Text;
using Bing.Helpers;
using Newtonsoft.Json;

namespace Bing.Caching.CSRedis;

/// <summary>
/// CSRedis 缓存管理
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class CSRedisCacheManager : ICache
{
    /// <summary>
    /// 是否存在指定键的缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => await RedisHelper.ExistsAsync(key);

    /// <summary>
    /// 从缓存中获取数据，如果不存在，则执行获取数据操作并添加到缓存中
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="func">获取数据操作</param>
    /// <param name="expiration">过期时间间隔</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> func, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (await RedisHelper.ExistsAsync(key))
            return await RedisHelper.GetAsync<T>(key);
        var result = await func();
        await RedisHelper.SetAsync(key, result, GetExpiration(expiration));
        return result;
    }

    /// <summary>
    /// 从缓存中获取数据
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="type">缓存数据类型</param>
    /// <param name="cancellationToken">取消令牌</param>
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

    /// <summary>
    /// 从缓存中获取数据
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default) => await RedisHelper.GetAsync<T>(key);

    /// <summary>
    /// 当缓存数据不存在则添加，已存在不会添加，添加成功返回true
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<bool> TryAddAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => await RedisHelper.SetAsync(key, value, GetExpiration(expiration));

    /// <summary>
    /// 添加缓存。如果已存在缓存，将覆盖
    /// </summary>
    /// <typeparam name="T">缓存数据类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiration">过期时间间隔</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AddAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => await RedisHelper.SetAsync(key, value, GetExpiration(expiration));

    /// <summary>
    /// 移除缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) => await RedisHelper.DelAsync(key);

    /// <summary>
    /// 通过缓存键前缀移除缓存
    /// </summary>
    /// <param name="prefix">缓存键前缀</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        Check.NotNullOrEmpty(prefix, nameof(prefix));
        prefix = this.HandlePrefix(prefix);
        var redisKeys = this.SearchRedisKeys(prefix);
        await RedisHelper.DelAsync(redisKeys);
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ClearAsync(CancellationToken cancellationToken = default) => await RedisHelper.NodesServerManager.FlushDbAsync();
}
