using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore;

/// <summary>
/// 服务提供程序(<see cref="IServiceProvider"/>) 扩展
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// 获取HttpContext实例
    /// </summary>
    /// <param name="provider">服务提供程序</param>
    public static HttpContext HttpContext(this IServiceProvider provider)
    {
        var accessor = provider.GetService<IHttpContextAccessor>();
        return accessor?.HttpContext;
    }

    /// <summary>
    /// 当前业务是否处于HttpRequest中
    /// </summary>
    /// <param name="provider">服务提供程序</param>
    public static bool InHttpRequest(this IServiceProvider provider)
    {
        var context = provider.HttpContext();
        return context != null;
    }
}