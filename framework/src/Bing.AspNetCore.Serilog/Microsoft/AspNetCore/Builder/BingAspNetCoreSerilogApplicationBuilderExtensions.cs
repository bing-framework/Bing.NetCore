using Bing.AspNetCore.Serilog;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
    /// </summary>
    public static class BingAspNetCoreSerilogApplicationBuilderExtensions
    {
        /// <summary>
        /// 注册Serilog Enricher的日志信息中间件
        /// </summary>
        /// <param name="app">应用程序构建器</param>
        public static IApplicationBuilder UseBingSerilogEnrichers(this IApplicationBuilder app)
        {
            return app.UseMiddleware<BingSerilogMiddleware>();
        }
    }
}
