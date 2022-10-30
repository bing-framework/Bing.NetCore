using System;
using System.Threading.Tasks;
using Bing.ExceptionHandling;
using Bing.Helpers;
using Bing.Http;
using Bing.Utils.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bing.AspNetCore.ExceptionHandling;

/// <summary>
/// 异常处理中间件
/// </summary>
public class BingExceptionHandlingMiddleware : IMiddleware
{
    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<BingExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// 清空缓存头委托
    /// </summary>
    private readonly Func<object, Task> _clearCacheHeaderDelegate;

    /// <summary>
    /// 初始化一个<see cref="BingExceptionHandlingMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="logger">日志</param>
    public BingExceptionHandlingMiddleware(RequestDelegate next, ILogger<BingExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _clearCacheHeaderDelegate = ClearCacheHeaders;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception e)
        {
            // 如果响应已经开始，我们什么也不能做，只能中止
            if(context.Response.HasStarted)
            {
                _logger.LogWarning("An exception occurred, but response has already started!");
                throw;
            }
            await HandleAndWarpException(context, e);
        }
    }

    /// <summary>
    /// 处理并包装异常
    /// </summary>
    /// <param name="httpContext">Http上下文</param>
    /// <param name="exception">异常</param>
    private async Task HandleAndWarpException(HttpContext httpContext, Exception exception)
    {
        _logger.LogException(exception);

        var errorInfoConverter = httpContext.RequestServices.GetRequiredService<IExceptionToErrorInfoConverter>();
        var statusCodeFinder = httpContext.RequestServices.GetRequiredService<IHttpExceptionStatusCodeFinder>();
        var options = httpContext.RequestServices.GetRequiredService<IOptions<BingExceptionHandlingOptions>>().Value;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = (int)statusCodeFinder.GetStatusCode(httpContext, exception);
        httpContext.Response.OnStarting(_clearCacheHeaderDelegate, httpContext.Response);
        httpContext.Response.Headers.Add(BingHttpConst.BingErrorFormat, "true");

        var remoteServiceErrorInfo = errorInfoConverter.Convert(exception, options.SendExceptionDetailsToClients);
        await WriteJsonAsync(httpContext.Response, new {code = Conv.ToInt(remoteServiceErrorInfo.Code), message = remoteServiceErrorInfo.Message});

        await httpContext.RequestServices.GetRequiredService<IExceptionNotifier>().NotifyAsync(new ExceptionNotificationContext(exception));
    }

    /// <summary>
    /// 写入Json
    /// </summary>
    /// <param name="response">Http响应</param>
    /// <param name="obj">对象</param>
    private static async Task WriteJsonAsync(HttpResponse response, object obj)
    {
        var json = JsonHelper.ToJson(obj);
        response.ContentType = "application/json; charset=utf-8";
        await response.WriteAsync(json);
    }

    /// <summary>
    /// 清空缓存头
    /// </summary>
    /// <param name="state">状态</param>
    private Task ClearCacheHeaders(object state)
    {
        var response = (HttpResponse)state;
        response.Headers[HeaderNames.CacheControl] = "no-cache";
        response.Headers[HeaderNames.Pragma] = "no-cache";
        response.Headers[HeaderNames.Expires] = "-1";
        response.Headers.Remove(HeaderNames.ETag);

        return Task.CompletedTask;
    }
}