using System.Diagnostics;
using Bing.AspNetCore;
using Bing.AspNetCore.ExceptionHandling;
using Bing.AspNetCore.Security.Claims;
using Bing.AspNetCore.Tracing;
using Bing.Core.Builders;
using Bing.Logging;
using Bing.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
/// </summary>
public static partial class BingApplicationBuilderExtensions
{
    /// <summary>
    /// 异常处理中间件标识
    /// </summary>
    private const string ExceptionHandlingMiddlewareMarker = "_BingExceptionHandlingMiddleware_Added";

    /// <summary>
    /// 框架初始化
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
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.UseMiddleware<BingCorrelationIdMiddleware>();

    /// <summary>
    /// 注册异常日志中间件
    /// </summary>
    /// <param name="app">应用程序构建器</param>
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
    public static IApplicationBuilder UseBingClaimsMap(this IApplicationBuilder app) => app.UseMiddleware<BingClaimsMapMiddleware>();

    /// <summary>
    /// Bing框架初始化，适用于AspNetCore环境
    /// </summary>
    /// <param name="app">应用程序构建器</param>
    public static IApplicationBuilder UseBing(this IApplicationBuilder app)
    {
        var provider = app.ApplicationServices;
        var logger = provider.GetLogger(FrameworkLog);
        logger.LogInformation("Bing 框架初始化开始...");
        var watch = new Stopwatch();
        watch.Start();
        try
        {
            // 输出初始化日志
            //var startupLogger = provider.GetService<StartupLogger>();
            //startupLogger.Output(provider);

            var modules = provider.GetAllModules();
            logger.LogInformation("共发现 {ModuleCount} 个模块需要初始化。", modules.Length);
            foreach (var module in modules)
            {
                var moduleType = module.GetType();
                var moduleName = Reflections.GetDescription(moduleType) ?? moduleType.Name;
                logger.LogInformation("正在初始化模块: {ModuleName} ({ModuleType})", moduleName, moduleType.Name);
                if (module is AspNetCoreBingModule netCoreModule)
                    netCoreModule.UseModule(app);
                else
                    module.UseModule(provider);
                logger.LogInformation("模块 {ModuleName} ({ModuleType}) 初始化完成", moduleName, moduleType.Name);
            }
            watch.Stop();
            logger.LogInformation("Bing 框架初始化完成，耗时: {ElapsedTime}", watch.Elapsed);
            return app;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bing 框架初始化失败: {ErrorMessage}", ex.Message);
            throw;
        }
    }
}
