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
    /// <param name="services">用于注册限流服务的服务集合。</param>
    /// <param name="capacity">每个令牌桶的容量。</param>
    /// <param name="refillTokensPerSecond">每秒补充的令牌数。</param>
    /// <param name="idleTimeout">空闲令牌桶的清理时限。</param>
    /// <param name="maxKeys">保留的最大限流键数量。</param>
    /// <param name="configure">限流选项配置委托；为 null 时保留默认值。</param>
    /// <returns>注册后的服务集合。</returns>
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
    /// <param name="app">要加入令牌桶限流中间件的应用构建器。</param>
    /// <returns>已加入中间件的应用构建器。</returns>
    public static IApplicationBuilder UseBingRateLimiting(this IApplicationBuilder app) =>
        app.UseMiddleware<BingRateLimitMiddleware>();
}
