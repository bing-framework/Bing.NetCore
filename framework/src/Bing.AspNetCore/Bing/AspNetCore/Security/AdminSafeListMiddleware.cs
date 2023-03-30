using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bing.AspNetCore.Security;

/// <summary>
/// 安全管理列表中间件
/// </summary>
public class AdminSafeListMiddleware : IMiddleware
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<AdminSafeListMiddleware> _logger;

    /// <summary>
    /// 方法
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    /// IP白名单
    /// </summary>
    private readonly string _whitelist;

    /// <summary>
    /// 初始化一个<see cref="AdminSafeListMiddleware"/>类型的实例
    /// </summary>
    /// <param name="next">方法</param>
    /// <param name="whitelist">IP白名单</param>
    /// <param name="logger">日志</param>
    public AdminSafeListMiddleware(RequestDelegate next, string whitelist, ILogger<AdminSafeListMiddleware> logger)
    {
        _next = next;
        _whitelist = whitelist;
        _logger = logger;
    }

    /// <summary>
    /// 执行中间件拦截逻辑
    /// </summary>
    /// <param name="context">Http上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method != "GET")
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            if(_logger.IsEnabled(LogLevel.Trace)) 
                _logger.LogTrace($"来自远程IP地址的请求：{remoteIp}");

            var ips = _whitelist.Split(';');
            var bytes = remoteIp.GetAddressBytes();
            var badIp = true;
            foreach (var ip in ips)
            {
                var testIp = IPAddress.Parse(ip);
                if (testIp.GetAddressBytes().SequenceEqual(bytes))
                {
                    badIp = false;
                    break;
                }
            }

            if (badIp)
            {
                _logger.LogWarning($"来自远程IP地址的禁止请求：{remoteIp}");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }
        }

        await _next.Invoke(context);
    }
}
