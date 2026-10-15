using Bing.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore.RateLimiting;

/// <summary>
/// 注册 HTTP 令牌桶限流能力。
/// </summary>
public static class BingRateLimitExtensions
{
    /// <summary>
    /// 注册本地 HTTP 令牌桶。
    /// </summary>
    public static IServiceCollection AddBingLocalRateLimiting(this IServiceCollection services,
        int capacity, double refillTokensPerSecond, TimeSpan idleTimeout, int maxKeys,
        Action<BingRateLimitOptions> configure = null)
    {
        services.TryAddSingleton<ITokenBucketRateLimiter>(_ =>
            new LocalTokenBucketRateLimiter(capacity, refillTokensPerSecond, idleTimeout, maxKeys));
        services.AddOptions<BingRateLimitOptions>();
        if (configure != null)
            services.Configure(configure);
        return services;
    }

    /// <summary>
    /// 将令牌桶限流加入 HTTP 管道。
    /// </summary>
    public static IApplicationBuilder UseBingRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<BingRateLimitMiddleware>();
}
