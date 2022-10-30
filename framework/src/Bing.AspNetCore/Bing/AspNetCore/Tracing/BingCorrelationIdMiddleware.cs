using System.Threading.Tasks;
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
        var correlationId = _correlationIdProvider.Get();
        TraceIdContext.Current ??= new TraceIdContext(correlationId);
        try
        {
            // TODO: 由于可能存在某个中间件设置了Response导致当前中间件无法设置
            CheckAndSetCorrelationIdOnResponse(context, _options, correlationId);
            await _next(context);
        }
        finally
        {
            CheckAndSetCorrelationIdOnResponse(context, _options, correlationId);
        }
    }

    /// <summary>
    /// 检查并设置跟踪关联ID在响应内容
    /// </summary>
    /// <param name="httpContext">Http上下文</param>
    /// <param name="options">跟踪关联ID配置选项信息</param>
    /// <param name="correlationId">跟踪关联ID</param>
    protected virtual void CheckAndSetCorrelationIdOnResponse(HttpContext httpContext, CorrelationIdOptions options,
        string correlationId)
    {
        if (httpContext.Response.HasStarted)
            return;
        if (!options.SetResponseHeader)
            return;
        if (httpContext.Response.Headers.ContainsKey(options.HttpHeaderName))
            return;
        httpContext.Response.Headers[options.HttpHeaderName] = correlationId;
    }
}