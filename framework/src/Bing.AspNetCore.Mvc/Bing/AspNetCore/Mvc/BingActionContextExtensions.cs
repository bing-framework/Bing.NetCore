using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

/// <summary>
/// 操作上下文扩展
/// </summary>
internal static class BingActionContextExtensions
{
    /// <summary>
    /// 获取请求服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="context">过滤器上下文</param>
    /// <returns>从请求服务容器中获取的指定服务。</returns>
    public static T GetRequiredService<T>(this FilterContext context) where T : class => context.HttpContext.RequestServices.GetRequiredService<T>();

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="context">过滤器上下文</param>
    /// <param name="defaultValue">未找到服务时返回的默认值。</param>
    /// <returns>从请求服务容器中获取的指定服务；未找到时返回默认值。</returns>
    public static T GetService<T>(this FilterContext context, T defaultValue = default) where T : class => context.HttpContext.RequestServices.GetService<T>() ?? defaultValue;
}
