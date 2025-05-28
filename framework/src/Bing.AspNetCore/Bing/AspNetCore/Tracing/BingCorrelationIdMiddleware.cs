using Bing.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.Tracing;

/// <summary>
/// 跟踪标识中间件
/// </summary>
public class BingCorrelationIdMiddleware : IMiddleware
{
    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 跟踪关联ID配置选项信息
    /// </summary>
    private readonly CorrelationIdOptions _options;

    /// <summary>
    /// 跟踪关联ID提供程序
    /// </summary>
    private readonly ICorrelationIdProvider _correlationIdProvider;

    /// <summary>
    /// 初始化一个<see cref="BingCorrelationIdMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="options">跟踪关联ID配置选项信息</param>
    /// <param name="correlationIdProvider">跟踪关联ID提供程序</param>
    public BingCorrelationIdMiddleware(RequestDelegate next, IOptions<CorrelationIdOptions> options, ICorrelationIdProvider correlationIdProvider)
    {
        _next = next;
        _options = options.Value;
        _correlationIdProvider = correlationIdProvider;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationIdFromRequest(context);
        TraceIdContext.Current ??= new TraceIdContext(correlationId);
        using (_correlationIdProvider.Change(correlationId))
        {
            CheckAndSetCorrelationIdOnResponse(context, _options, correlationId);
            await _next(context);
        }
    }

    /// <summary>
    /// 从HTTP请求中获取关联ID。
    /// </summary>
    /// <param name="context">当前HTTP请求的上下文</param>
    /// <returns>返回一个字符串表示的关联ID。如果请求中没有找到关联ID，将生成一个新的GUID作为关联ID，并添加到请求头中。</returns>
    protected virtual string GetCorrelationIdFromRequest(HttpContext context)
    {
        var correlationId = context.Request.Headers[_options.HttpHeaderName];
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            context.Request.Headers[_options.HttpHeaderName] = correlationId;
        }
        return correlationId;
    }

    /// <summary>
    /// 检查并设置跟踪关联ID在响应内容
    /// </summary>
    /// <param name="httpContext">Http上下文</param>
    /// <param name="options">跟踪关联ID配置选项信息</param>
    /// <param name="correlationId">跟踪关联ID</param>
    protected virtual void CheckAndSetCorrelationIdOnResponse(HttpContext httpContext, CorrelationIdOptions options, string correlationId)
    {
        httpContext.Response.OnStarting(() =>
        {
            if (options.SetResponseHeader && !httpContext.Response.Headers.ContainsKey(options.HttpHeaderName) && !string.IsNullOrWhiteSpace(correlationId))
                httpContext.Response.Headers[options.HttpHeaderName] = correlationId;
            return Task.CompletedTask;
        });
    }
}
