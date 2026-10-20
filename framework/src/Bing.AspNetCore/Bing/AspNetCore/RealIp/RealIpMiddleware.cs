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
    /// 已解析的可信代理地址。
    /// </summary>
    private readonly IReadOnlyList<IPAddress> _trustedProxies;

    /// <summary>
    /// 初始化一个 <see cref="RealIpMiddleware" /> 类型的实例。
    /// </summary>
    /// <param name="next">后续请求委托。</param>
    /// <param name="options">真实 IP 配置。</param>
    /// <param name="logger">日志记录器。</param>
    public RealIpMiddleware(RequestDelegate next, IOptions<RealIpOptions> options, ILogger<RealIpMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(_options.HeaderKey))
            throw new ArgumentException("真实 IP 请求头名称不能为空。", nameof(options));
        _trustedProxies = ParseTrustedProxies(_options.TrustedProxies);
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        try
        {
            var ip = TryGetIpAddress(context.Connection.RemoteIpAddress, headers, _options.HeaderKey);
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
    /// <param name="remoteIpAddress">请求连接的对端地址。</param>
    /// <param name="headers">请求头字典</param>
    /// <param name="key">请求头</param>
    /// <returns>解析出的 IP 地址；请求头不存在或值无法解析时返回 null。</returns>
    private IPAddress TryGetIpAddress(IPAddress remoteIpAddress, IHeaderDictionary headers, string key)
    {
        if (remoteIpAddress == null || !_trustedProxies.Any(proxy => AreSameAddress(proxy, remoteIpAddress)))
        {
            WriteLog("请求对端不在可信代理列表中，忽略真实 IP 请求头。");
            return null;
        }

        if (headers.TryGetValue(key, out var values))
        {
            _logger.LogTrace("解析真实 IP 请求头，共 {HeaderValueCount} 个值。", values.Count);
            var candidates = values
                .SelectMany(value => value.Split(','))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value) &&
                                !string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            for (var index = candidates.Length - 1; index >= 0; index--)
            {
                if (!IPAddress.TryParse(candidates[index], out var ipAddress))
                {
                    _logger.LogError("解析真实 IP 失败，转发链中存在无效地址。");
                    continue;
                }

                if (_trustedProxies.Any(proxy => AreSameAddress(proxy, ipAddress)))
                    continue;

                WriteLog($"解析真实IP成功: {ipAddress}");
                return ipAddress;
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

    /// <summary>
    /// 解析可信代理地址配置。
    /// </summary>
    private static IReadOnlyList<IPAddress> ParseTrustedProxies(IEnumerable<string> values)
    {
        if (values == null)
            return Array.Empty<IPAddress>();

        var result = new List<IPAddress>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !IPAddress.TryParse(value.Trim(), out var address))
                throw new ArgumentException($"可信代理地址无效：{value}。", nameof(values));
            if (!result.Any(item => AreSameAddress(item, address)))
                result.Add(address);
        }
        return result;
    }

    /// <summary>
    /// 比较 IPv4 与 IPv4 映射 IPv6 地址。
    /// </summary>
    private static bool AreSameAddress(IPAddress left, IPAddress right)
    {
        if (left == null || right == null)
            return false;
        if (left.Equals(right))
            return true;
        if (left.IsIPv4MappedToIPv6)
            return left.MapToIPv4().Equals(right);
        if (right.IsIPv4MappedToIPv6)
            return right.MapToIPv4().Equals(left);
        return false;
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
    /// <remarks>默认使用 <c>X-Forwarded-For</c>。只有连接对端位于 <see cref="TrustedProxies"/> 时才会读取该请求头。</remarks>
    public string HeaderKey { get; set; } = "X-Forwarded-For";

    /// <summary>
    /// 允许提交真实 IP 请求头的代理地址。
    /// </summary>
    /// <remarks>为空时不信任任何转发头；地址比较支持 IPv4 与 IPv4 映射 IPv6。</remarks>
    public IList<string> TrustedProxies { get; } = new List<string>();
}

/// <summary>
/// 真实IP过滤器
/// </summary>
public class RealIpFilter : IStartupFilter
{
    /// <inheritdoc />
    /// <remarks>
    /// 配置真实客户端 IP 中间件的请求管道。
    /// </remarks>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<RealIpMiddleware>();
        next(app);
    };
}
