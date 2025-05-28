using Bing.AspNetCore.Serilog;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// 应用程序构建器(<see cref="IApplicationBuilder"/>) 扩展
/// </summary>
public static class BingAspNetCoreSerilogApplicationBuilderExtensions
{
    /// <summary>
    /// 注册Serilog Enrichers的日志信息中间件
    /// </summary>
    /// <remarks>
    /// 【重要】必须在 IApplicationBuilder.UseAuthentication() 之后注册该中间件，否则无法获取到当前用户信息。
    /// </remarks>
    /// <param name="app">应用程序构建器</param>
    public static IApplicationBuilder UseBingSerilogEnrichers(this IApplicationBuilder app) => app.UseMiddleware<BingSerilogMiddleware>();
}
