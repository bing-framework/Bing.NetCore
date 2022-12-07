using System;
using Bing.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.WebClientInfo;

/// <summary>
/// 基于Http上下文的Web客户端信息提供程序
/// </summary>
public class HttpContextWebClientInfoProvider : IWebClientInfoProvider, ITransientDependency
{
    /// <summary>
    /// 日志
    /// </summary>
    protected ILogger<HttpContextWebClientInfoProvider> Logger { get; }

    /// <summary>
    /// Http上下文访问器
    /// </summary>
    protected IHttpContextAccessor HttpContextAccessor { get; }

    /// <summary>
    /// 初始化一个<see cref="HttpContextWebClientInfoProvider"/>类型的实例
    /// </summary>
    /// <param name="logger">日志</param>
    /// <param name="httpContextAccessor">Http上下文访问器</param>
    public HttpContextWebClientInfoProvider(ILogger<HttpContextWebClientInfoProvider> logger
        , IHttpContextAccessor httpContextAccessor)
    {
        Logger = logger;
        HttpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// 浏览器信息
    /// </summary>
    public string BrowserInfo => GetBrowserInfo();

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    public string ClientIpAddress => GetClientIpAddress();

    /// <summary>
    /// 获取浏览器信息
    /// </summary>
    protected virtual string GetBrowserInfo() => HttpContextAccessor.HttpContext?.Request?.Headers?["User-Agent"];

    /// <summary>
    /// 获取客户端IP地址
    /// </summary>
    protected virtual string GetClientIpAddress()
    {
        try
        {
            return HttpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        }
        catch (Exception e)
        {
            Logger.LogException(e, LogLevel.Warning);
            return null;
        }
    }
}