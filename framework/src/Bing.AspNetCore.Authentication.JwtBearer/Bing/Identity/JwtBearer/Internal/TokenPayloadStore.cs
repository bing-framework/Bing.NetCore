using Bing.Caching;
using Bing.Identity.JwtBearer.Abstractions;

namespace Bing.Identity.JwtBearer.Internal;

/// <summary>
/// 令牌Payload存储器
/// </summary>
internal sealed class TokenPayloadStore : ITokenPayloadStore
{
    /// <summary>
    /// 存储令牌负载并按过期时间设置有效期的缓存提供程序。
    /// </summary>
    private readonly ICache _cache;

    /// <summary>
    /// 初始化一个 <see cref="TokenPayloadStore"/> 类型的实例。
    /// </summary>
    /// <param name="cache">用于存储令牌负载的缓存。</param>
    public TokenPayloadStore(ICache cache) => _cache = cache;

    /// <inheritdoc />
    public async Task SaveAsync(string token, IDictionary<string, string> payload, DateTime expires) =>
        await _cache.SetAsync(GetPayloadKey(token), payload, new CacheOptions() { Expiration = expires.Subtract(DateTime.UtcNow) });

    /// <inheritdoc />
    public async Task RemoveAsync(string token)
    {
        if (!await _cache.ExistsAsync(GetPayloadKey(token)))
            return;
        await _cache.RemoveAsync(GetPayloadKey(token));
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, string>> GetAsync(string token) =>
        await _cache.GetAsync<IDictionary<string, string>>(GetPayloadKey(token));

    /// <summary>
    /// 获取Payload缓存键
    /// </summary>
    /// <param name="token">令牌</param>
    /// <returns>用于存储令牌负载的缓存键。</returns>
    private static string GetPayloadKey(string token) => $"jwt:token:payload:{token}";
}
