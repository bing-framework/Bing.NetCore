using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.AspNetCore.RealIp;

/// <summary>
/// 远程IP中间件
/// </summary>
/// <remarks>nginx 代理服务的时候需要使用才能通过RemoteIpAddress获取客户端真实IP</remarks>
public class RealIpMiddleware : IMiddleware
{
    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// 真实IP选项
    /// </summary>
    private readonly RealIpOptions _options;

    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<RealIpMiddleware> _logger;

    /// <summary>
    /// 初始化一个<see cref="RealIpMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="options">真实IP选项</param>
    /// <param name="logger">日志</param>
    public RealIpMiddleware(RequestDelegate next, IOptions<RealIpOptions> options, ILogger<RealIpMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        try
        {
            var ip = TryGetIpAddress(headers, _options.HeaderKey) ?? TryGetIpAddress(headers, "x-forwarded-for") ?? TryGetIpAddress(headers, "X-Forwarded-For");
            if (ip != null) 
                context.Connection.RemoteIpAddress = ip;
        }
        finally
        {
            await _next(context);
        }
    }

    /// <summary>
    /// 尝试获取IP地址
    /// </summary>
    /// <param name="headers">请求头字典</param>
    /// <param name="key">请求头</param>
    /// <returns>解析出的 IP 地址；请求头不存在或值无法解析时返回 null。</returns>
    private IPAddress TryGetIpAddress(IHeaderDictionary headers, string key)
    {
        if (headers.ContainsKey(key))
        {
            headers.TryGetValue(key, out var ip);

            WriteLog($"解析真实IP地址: {ip}");
            if (string.IsNullOrEmpty(ip) == false && ip.ToString().ToLower() != "unknown")
            {
                var tmpIp = key.Equals("x-forwarded-for", StringComparison.CurrentCultureIgnoreCase)
                    ? ip.ToString().Split(',')[0]
                    : ip.ToString();
                if (IPAddress.TryParse(tmpIp, out var ipAddress))
                {
                    WriteLog($"解析真实IP成功: {ipAddress}");
                    return ipAddress;
                }

                _logger.LogError($"解析真实IP失败: {tmpIp}");
            }
        }

        return null;
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    /// <param name="message">消息</param>
    private void WriteLog(string message)
    {
        if (_logger.IsEnabled(LogLevel.Trace)==false)
            return;
        _logger.LogTrace(message);
    }
}

/// <summary>
/// 配置真实客户端 IP 所在的请求头名称。
/// </summary>
public class RealIpOptions
{
    /// <summary>
    /// 获取或设置承载真实客户端 IP 的请求头名称。
    /// </summary>
    /// <remarks>仅应在请求经过可信代理并且应用已建立请求头信任边界时启用对应配置，否则请求方可伪造该值。</remarks>
    public string HeaderKey { get; set; }
}

/// <summary>
/// 真实IP过滤器
/// </summary>
public class RealIpFilter : IStartupFilter
{
    /// <summary>
    /// 配置真实客户端 IP 中间件的请求管道。
    /// </summary>
    /// <param name="next">后续请求管道委托。</param>
    /// <returns>已添加真实 IP 中间件的请求管道配置委托。</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<RealIpMiddleware>();
        next(app);
    };
}
