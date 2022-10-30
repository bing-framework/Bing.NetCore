using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Caching;

namespace Bing.Permissions.Identity.JwtBearer.Internal;

/// <summary>
/// 令牌Payload存储器
/// </summary>
internal sealed class TokenPayloadStore : ITokenPayloadStore
{
    /// <summary>
    /// 缓存
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 初始化一个<see cref="TokenPayloadStore"/>类型的实例
    /// </summary>
    /// <param name="cache"></param>
    public TokenPayloadStore(ICache cache) => _cache = cache;

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="token">令牌</param>
    /// <param name="payload">负载字典</param>
    /// <param name="expires">过期时间</param>
    public async Task SaveAsync(string token, IDictionary<string, string> payload, DateTime expires) =>
        await _cache.AddAsync(GetPayloadKey(token), payload, expires.Subtract(DateTime.UtcNow));

    /// <summary>
    /// 移除
    /// </summary>
    /// <param name="token">令牌</param>
    public async Task RemoveAsync(string token)
    {
        if (!await _cache.ExistsAsync(GetPayloadKey(token)))
            return;
        await _cache.RemoveAsync(GetPayloadKey(token));
    }

    /// <summary>
    /// 获取Payload
    /// </summary>
    /// <param name="token">令牌</param>
    public async Task<IDictionary<string, string>> GetAsync(string token) =>
        await _cache.GetAsync<IDictionary<string, string>>(GetPayloadKey(token));

    /// <summary>
    /// 获取Payload缓存键
    /// </summary>
    /// <param name="token">令牌</param>
    private static string GetPayloadKey(string token) => $"jwt:token:payload:{token}";
}