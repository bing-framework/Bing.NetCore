using System.Diagnostics;
using Bing.AspNetCore;
using Bing.AspNetCore.ExceptionHandling;
using Bing.AspNetCore.Tracing;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder
{
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
        /// 添加MVC并支持Area路由
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        /// <param name="area">是否支持Area路由</param>
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
        /// 注册跟踪标识中间件
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) => app.UseMiddleware<BingCorrelationIdMiddleware>();

        /// <summary>
        /// Bing框架初始化，适用于AspNetCore环境
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseBing(this IApplicationBuilder app)
        {
            var provider = app.ApplicationServices;
            var logger = provider.GetLogger(FrameworkLog);
            logger.LogInformation("Bing框架初始化开始");
            var watch = Stopwatch.StartNew();
            var modules = provider.GetAllModules();
            foreach (var module in modules)
            {
                var moduleName = Reflections.GetDescription(module.GetType());
                logger.LogInformation($"正在初始化模块 “{moduleName}”");
                 if (module is AspNetCoreBingModule netCoreModule)
                    netCoreModule.UseModule(app);
                else
                    module.UseModule(provider);
                logger.LogInformation($"模块 “{moduleName}” 初始化完成");
            }
            watch.Stop();
            logger.LogInformation($"Bing框架初始化完成，耗时：{watch.Elapsed}");
            return app;
        }
    }
}
