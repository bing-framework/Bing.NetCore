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

    /// <inheritdoc />
    public string BrowserInfo => GetBrowserInfo();

    /// <inheritdoc />
    public string ClientIpAddress => GetClientIpAddress();

    /// <inheritdoc />
    public string DeviceInfo => string.Empty;

    /// <summary>
    /// 获取浏览器信息
    /// </summary>
    /// <returns>当前请求的 User-Agent 信息；无请求上下文时返回 null。</returns>
    protected virtual string GetBrowserInfo() => HttpContextAccessor.HttpContext?.Request?.Headers?["User-Agent"];

    /// <summary>
    /// 获取客户端IP地址
    /// </summary>
    /// <returns>当前请求的客户端 IP 地址；无法获取时返回 null。</returns>
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
