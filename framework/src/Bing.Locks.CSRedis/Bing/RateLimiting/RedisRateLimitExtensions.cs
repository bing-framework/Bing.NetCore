using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.RateLimiting;

/// <summary>
/// 注册集群共享令牌桶。
/// </summary>
public static class RedisRateLimitExtensions
{
    /// <summary>
    /// 注册 Redis 令牌桶限流器。
    /// </summary>
    public static IServiceCollection AddBingRedisRateLimiting(this IServiceCollection services,
        string policyName, int capacity, double refillTokensPerSecond, TimeSpan idleTimeout, int maxKeys)
    {
        services.AddOptions();
        services.TryAddSingleton<ITokenBucketRateLimiter>(_ =>
            new RedisTokenBucketRateLimiter(policyName, capacity, refillTokensPerSecond, idleTimeout, maxKeys));
        return services;
    }
}
