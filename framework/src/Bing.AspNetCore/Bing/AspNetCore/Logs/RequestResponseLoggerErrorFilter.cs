using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// 请求响应记录器 错误过滤器
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequestResponseLoggerErrorFilter : Attribute, IExceptionFilter
{
    /// <summary>
    /// 获取请求响应日志
    /// </summary>
    /// <param name="context">Http上下文</param>
    /// <returns>当前请求对应的请求响应日志。</returns>
    private RequestResponseLog GetLog(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IRequestResponseLogCreator>().Log;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        var log = GetLog(context.HttpContext);
        log.IsExceptionActionLevel = true;
        log.RequestDateTimeUtcActionLevel ??= DateTime.UtcNow;
    }
}