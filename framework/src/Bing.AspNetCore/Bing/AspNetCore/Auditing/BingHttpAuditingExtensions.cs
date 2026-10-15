using Bing.Auditing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Auditing;

/// <summary>
/// 注册和启用 HTTP 审计。
/// </summary>
public static class BingHttpAuditingExtensions
{
    /// <summary>
    /// 注册 HTTP 审计服务。
    /// </summary>
    public static IServiceCollection AddBingHttpAuditing(this IServiceCollection services,
        Action<BingHttpAuditingOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        services.AddBingAuditing();
        services.AddOptions<BingHttpAuditingOptions>();
        if (configure != null)
            services.Configure(configure);
        return services;
    }

    /// <summary>
    /// 将 HTTP 审计中间件加入请求管道。
    /// </summary>
    /// <remarks>应在 UseRouting 之后调用，以便识别 Endpoint 上的 DisableAuditingAttribute。</remarks>
    public static IApplicationBuilder UseBingHttpAuditing(this IApplicationBuilder app) =>
        app?.UseMiddleware<BingHttpAuditingMiddleware>() ?? throw new ArgumentNullException(nameof(app));
}
