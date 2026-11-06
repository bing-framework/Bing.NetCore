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
    /// <param name="services">用于注册 HTTP 审计服务的服务集合。</param>
    /// <param name="configure">HTTP 审计选项配置委托；为 null 时保留默认值。</param>
    /// <returns>注册后的服务集合。</returns>
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
    /// <param name="app">要加入 HTTP 审计中间件的应用构建器。</param>
    /// <returns>已加入中间件的应用构建器。</returns>
    /// <remarks>应在 UseRouting 之后调用，以便识别 Endpoint 上的 DisableAuditingAttribute。</remarks>
    public static IApplicationBuilder UseBingHttpAuditing(this IApplicationBuilder app) =>
        app?.UseMiddleware<BingHttpAuditingMiddleware>() ?? throw new ArgumentNullException(nameof(app));
}
