using Bing.AspNetCore.Mvc.UI.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc.UI.Extensions;

/// <summary>
/// 服务集合(<see cref="IServiceCollection"/>)扩展
/// </summary>
public static class BingServiceCollectionExtensions
{
    /// <summary>
    /// 注册Razor静态Html生成器
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>已注册 Razor HTML 生成服务的服务集合。</returns>
    public static IServiceCollection AddRazorHtml(this IServiceCollection services)
    {
        services.AddScoped<IRouteAnalyzer, RouteAnalyzer>();
        services.AddScoped<IRazorHtmlGenerator, DefaultRazorHtmlGenerator>();
        return services;
    }
}