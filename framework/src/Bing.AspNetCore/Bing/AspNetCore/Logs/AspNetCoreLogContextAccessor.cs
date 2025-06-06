﻿using Bing.AspNetCore.WebClientInfo;
using Bing.DependencyInjection;
using Bing.Logging;
using Bing.Tracing;
using Bing.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore.Logs;

/// <summary>
/// AspNetCore日志上下文访问器
/// </summary>
[Dependency(ServiceLifetime.Scoped, ReplaceExisting = true)]
public class AspNetCoreLogContextAccessor : LogContextAccessor
{
    /// <summary>
    /// Http上下文访问器
    /// </summary>
    protected IHttpContextAccessor HttpContextAccessor { get; }

    /// <summary>
    /// Web客户端信息提供程序
    /// </summary>
    protected IWebClientInfoProvider WebClientInfoProvider { get; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser { get; }

    /// <summary>
    /// 初始化一个<see cref="AspNetCoreLogContextAccessor"/>类型的实例
    /// </summary>
    /// <param name="webClientInfoProvider">Web客户端信息提供程序</param>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    /// <param name="currentUser">当前用户</param>
    public AspNetCoreLogContextAccessor(
        IHttpContextAccessor httpContextAccessor,
        IWebClientInfoProvider webClientInfoProvider,
        ICurrentUser currentUser)
    {
        HttpContextAccessor = httpContextAccessor;
        WebClientInfoProvider = webClientInfoProvider;
        CurrentUser = currentUser;
    }

    /// <summary>
    /// 创建日志上下文
    /// </summary>
    protected override LogContext Create()
    {
        var context = base.Create();
        context.Ip = WebClientInfoProvider.ClientIpAddress;
        context.Browser = WebClientInfoProvider.ClientIpAddress;
        context.Url = HttpContextAccessor.HttpContext?.Request?.GetDisplayUrl();
        context.IsWebEnv = HttpContextAccessor.HttpContext?.Request != null;
        context.UserId = CurrentUser.UserId;
        context.TenantId = CurrentUser.TenantId;
        context.Application = CurrentUser.GetApplicationName();
        context.Data["UserName"] = CurrentUser.GetUserName();
        context.Data["FullName"] = CurrentUser.GetFullName();
        context.Data["TenantCode"] = CurrentUser.GetTenantCode();
        context.Data["TenantName"] = CurrentUser.GetTenantName();
        if (!context.IsWebEnv)
            context.TraceId = base.GetTraceId();
        return context;
    }

    /// <summary>
    /// 获取跟踪标识
    /// </summary>
    protected override string GetTraceId()
    {
        if (TraceIdContext.Current != null)
            return TraceIdContext.Current.TraceId;
        var correlationId = HttpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"];
        if (!string.IsNullOrWhiteSpace(correlationId))
            return correlationId;
        var traceId = HttpContextAccessor.HttpContext?.TraceIdentifier;
        return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : Guid.TryParse(traceId, out _) ? traceId : Guid.NewGuid().ToString("N");
    }
}
