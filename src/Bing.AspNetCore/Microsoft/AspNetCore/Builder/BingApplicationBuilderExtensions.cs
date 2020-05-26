using Bing.AspNetCore.ExceptionHandling;
using Bing.AspNetCore.Tracing;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class BingApplicationBuilderExtensions
    {
        /// <summary>
        /// 异常处理中间件标识
        /// </summary>
        private const string ExceptionHandlingMiddlewareMarker = "_BingExceptionHandlingMiddleware_Added";

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
    }
}
