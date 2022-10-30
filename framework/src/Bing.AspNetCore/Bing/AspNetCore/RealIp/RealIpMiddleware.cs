using System;
using System.Net;
using System.Threading.Tasks;
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
    private IPAddress TryGetIpAddress(IHeaderDictionary headers, string key)
    {
        if (headers.ContainsKey(key))
        {
            headers.TryGetValue(key, out var ip);
            _logger.LogDebug($"解析真实IP地址: {ip}");
            if (string.IsNullOrEmpty(ip) == false && ip.ToString().ToLower() != "unknown")
            {
                var tmpIp = key.Equals("x-forwarded-for", StringComparison.CurrentCultureIgnoreCase)
                    ? ip.ToString().Split(',')[0]
                    : ip.ToString();
                if (IPAddress.TryParse(tmpIp, out var ipAddress))
                {
                    _logger.LogDebug($"解析真实IP成功: {ipAddress}");
                    return ipAddress;
                }

                _logger.LogError($"解析真实IP失败: {tmpIp}");
            }
        }

        return null;
    }
}

/// <summary>
/// 真实IP选项
/// </summary>
public class RealIpOptions
{
    /// <summary>
    /// 请求头键名
    /// </summary>
    public string HeaderKey { get; set; }
}

/// <summary>
/// 真实IP过滤器
/// </summary>
public class RealIpFilter : IStartupFilter
{
    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="next">方法</param>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<RealIpMiddleware>();
        next(app);
    };
}