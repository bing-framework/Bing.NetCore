using Bing.Clients;
using Bing.DependencyInjection;
using Bing.Tracing;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace Bing.AspNetCore.Serilog;

/// <summary>
/// Serilog 中间件
/// </summary>
public class BingSerilogMiddleware : IMiddleware, ITransientDependency
{
    /// <summary>
    /// Serilog 选项配置
    /// </summary>
    private readonly BingAspNetCoreSerilogOptions _options;

    /// <summary>
    /// 下一个中间件
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 初始化一个<see cref="BingSerilogMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="options">Serilog 选项配置</param>
    public BingSerilogMiddleware(
        RequestDelegate next,
        IOptions<BingAspNetCoreSerilogOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var enrichers = new List<ILogEventEnricher>();

        AddUserEnrichers(context, enrichers);
        AddClientEnrichers(context, enrichers);
        AddCorrelationIdEnrichers(context, enrichers);

        if (enrichers.Count > 0)
        {
            using (LogContext.Push(enrichers.ToArray()))
                await _next(context);
        }
        else
        {
            await _next(context);
        }
    }

    /// <summary>
    /// 添加当前用户相关的日志事件增强器
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文</param>
    /// <param name="enrichers">日志事件增强器集合</param>
    private void AddUserEnrichers(HttpContext context, ICollection<ILogEventEnricher> enrichers)
    {
        var currentUser = context?.RequestServices?.GetRequiredService<ICurrentUser>();
        if (currentUser == null)
            return;
        if (!string.IsNullOrWhiteSpace(currentUser.TenantId))
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.TenantId, currentUser.TenantId));

        if (!string.IsNullOrWhiteSpace(currentUser.UserId))
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.UserId, currentUser.UserId));
    }

    /// <summary>
    /// 添加当前客户端相关的日志事件增强器
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文</param>
    /// <param name="enrichers">日志事件增强器集合</param>
    private void AddClientEnrichers(HttpContext context, ICollection<ILogEventEnricher> enrichers)
    {
        var currentClient = context?.RequestServices?.GetRequiredService<ICurrentClient>();
        if (currentClient == null)
            return;
        if (!string.IsNullOrWhiteSpace(currentClient.Id))
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.ClientId, currentClient.Id));
    }

    /// <summary>
    /// 添加关联ID相关的日志事件增强器
    /// </summary>
    /// <param name="context">当前 HTTP 请求上下文</param>
    /// <param name="enrichers">日志事件增强器集合</param>
    private void AddCorrelationIdEnrichers(HttpContext context, ICollection<ILogEventEnricher> enrichers)
    {
        var correlationIdProvider = context?.RequestServices?.GetRequiredService<ICorrelationIdProvider>();
        if (correlationIdProvider == null)
            return;
        var correlationId = correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(correlationId))
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.CorrelationId, correlationId));
    }
}
