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
    /// <param name="services"></param>
    public static IServiceCollection AddRazorHtml(this IServiceCollection services)
    {
        services.AddScoped<IRouteAnalyzer, RouteAnalyzer>();
        services.AddScoped<IRazorHtmlGenerator, DefaultRazorHtmlGenerator>();
        return services;
    }
}