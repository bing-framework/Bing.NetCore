using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Bing.AspNetCore.Middleware;

/// <summary>
/// 中间件基类
/// </summary>
public abstract class BingMiddlewareBase : IMiddleware
{
    /// <summary>
    /// 判断是否应该跳过某些指定的处理逻辑。
    /// </summary>
    /// <param name="context">当前HTTP请求的上下文</param>
    /// <param name="next">下一个请求委托</param>
    protected virtual Task<bool> ShouldSkipAsync(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();
        var controllerActionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var disableBingFeaturesAttribute = controllerActionDescriptor?.ControllerTypeInfo.GetCustomAttribute<DisableBingFeaturesAttribute>();
        return Task.FromResult(disableBingFeaturesAttribute != null && disableBingFeaturesAttribute.DisableMiddleware);
    }

    /// <inheritdoc />
    public abstract Task InvokeAsync(HttpContext context);
}
