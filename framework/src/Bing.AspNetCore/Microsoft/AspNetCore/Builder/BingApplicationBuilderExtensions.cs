using System.Diagnostics;
using Bing.AspNetCore;
using Bing.AspNetCore.ExceptionHandling;
using Bing.AspNetCore.Security.Claims;
using Bing.AspNetCore.Tracing;
using Bing.Core.Builders;
using Bing.Core.Modularity;
using Bing.Logging;
using Bing.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// 应用程序构建器扩展方法。
/// </summary>
public static partial class BingApplicationBuilderExtensions
{
    /// <summary>
    /// 异常处理中间件标识。
    /// </summary>
    private const string ExceptionHandlingMiddlewareMarker = "_BingExceptionHandlingMiddleware_Added";

    /// <summary>
    /// 框架初始化日志名称。
    /// </summary>
    private const string FrameworkLog = "BingFrameworkLog";

    /// <summary>
    /// 配置MVC路由，支持带区域（Area）的路由。
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <param name="area">是否启用区域（Area）支持，默认为 <c>true</c>。</param>
    /// <returns>返回 <see cref="IApplicationBuilder"/>，以支持流式 API 调用。</returns>
    public static IApplicationBuilder UseMvcWithAreaRoute(this IApplicationBuilder app, bool area = true)
    {
        return app.UseMvc(builder =>
        {
            if (area)
                builder.MapRoute("area", "{area:exists}/{controller}/{action=Index}/{id?}");
            builder.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
        });
    }

    /// <summary>
    /// 配置控制器路由，支持区域（Areas）和默认路由。
    /// </summary>
    /// <param name="endpoints">用于定义路由的 <see cref="IEndpointRouteBuilder"/>。</param>
    /// <param name="area">是否启用区域（Area）支持，默认为 <c>true</c>。</param>
    /// <returns>返回 <see cref="IEndpointRouteBuilder"/>，以支持流式 API 调用。</returns>
    public static IEndpointRouteBuilder MapControllersWithAreaRoute(this IEndpointRouteBuilder endpoints, bool area = true)
    {
        if (area)
        {
            endpoints.MapControllerRoute(
                name: "areas-router",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
        }
        endpoints.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        return endpoints;
    }

    /// <summary>
    /// 注册跟踪标识中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>返回原应用程序构建器，以支持继续配置中间件管道。</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.UseMiddleware<BingCorrelationIdMiddleware>();

    /// <summary>
    /// 注册异常日志中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>返回原应用程序构建器，以支持继续配置中间件管道。</returns>
    public static IApplicationBuilder UseBingExceptionHandling(this IApplicationBuilder app)
    {
        if (app.Properties.ContainsKey(ExceptionHandlingMiddlewareMarker))
            return app;
        app.Properties[ExceptionHandlingMiddlewareMarker] = true;
        return app.UseMiddleware<BingExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// 注册声明映射中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>返回原应用程序构建器，以支持继续配置中间件管道。</returns>
    public static IApplicationBuilder UseBingClaimsMap(this IApplicationBuilder app) => app.UseMiddleware<BingClaimsMapMiddleware>();

    /// <summary>
    /// Bing框架初始化，适用于AspNetCore环境
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    /// <returns>完成 Bing 模块初始化后的应用程序构建器。</returns>
    public static IApplicationBuilder UseBing(this IApplicationBuilder app)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));
        app.ApplicationServices.GetRequiredService<IBingModuleManager>().Initialize(CreateModuleContext(app));
        return app;
    }

    /// <summary>
    /// 异步初始化 Web 模块。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    /// <param name="cancellationToken">初始化取消令牌。</param>
    /// <returns>完成 Bing 模块初始化后的应用程序构建器。</returns>
    /// <remarks>调用完成后再启动请求处理。</remarks>
    public static async Task<IApplicationBuilder> UseBingAsync(this IApplicationBuilder app, CancellationToken cancellationToken = default)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));
        await app.ApplicationServices.GetRequiredService<IBingModuleManager>()
            .InitializeAsync(CreateModuleContext(app), cancellationToken).ConfigureAwait(false);
        return app;
    }

    /// <summary>
    /// 创建 Web 模块初始化上下文。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    /// <returns>绑定当前应用程序构建器的模块初始化上下文。</returns>
    private static BingModuleInitializationContext CreateModuleContext(IApplicationBuilder app) =>
        new(app.ApplicationServices, app, (module, provider) =>
        {
            if (module is AspNetCoreBingModule webModule) webModule.UseModule(app);
            else module.UseModule(provider);
        });
}
