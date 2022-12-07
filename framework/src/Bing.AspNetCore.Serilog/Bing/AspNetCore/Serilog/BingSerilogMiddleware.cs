using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Clients;
using Bing.DependencyInjection;
using Bing.Tracing;
using Bing.Users;
using Microsoft.AspNetCore.Http;
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
    /// 当前客户端
    /// </summary>
    private readonly ICurrentClient _currentClient;

    /// <summary>
    /// 当前用户
    /// </summary>
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 跟踪标识提供程序
    /// </summary>
    private readonly ICorrelationIdProvider _correlationIdProvider;

    /// <summary>
    /// Serilog 选项配置
    /// </summary>
    private readonly BingAspNetCoreSerilogOptions _options;

    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 初始化一个<see cref="BingSerilogMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="currentUser">当前用户</param>
    /// <param name="currentClient">当前客户端</param>
    /// <param name="correlationIdProvider">跟踪标识提供程序</param>
    /// <param name="options">Serilog 选项配置</param>
    public BingSerilogMiddleware(
        RequestDelegate next,
        ICurrentUser currentUser,
        ICurrentClient currentClient,
        ICorrelationIdProvider correlationIdProvider,
        IOptions<BingAspNetCoreSerilogOptions> options)
    {
        _next = next;
        _currentUser = currentUser;
        _currentClient = currentClient;
        _correlationIdProvider = correlationIdProvider;
        _options = options.Value;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var enrichers = new List<ILogEventEnricher>();
        if (_currentUser?.TenantId != null)
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.TenantId, _currentUser.TenantId));

        if (_currentUser?.UserId != null)
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.UserId, _currentUser.UserId));

        if (_currentClient?.Id != null)
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.ClientId, _currentClient.Id));

        var correlationId = _correlationIdProvider.Get();
        if (!string.IsNullOrEmpty(correlationId))
            enrichers.Add(new PropertyEnricher(_options.EnricherPropertyNames.CorrelationId, correlationId));

        using (LogContext.Push(enrichers.ToArray()))
            await _next(context);
    }
}