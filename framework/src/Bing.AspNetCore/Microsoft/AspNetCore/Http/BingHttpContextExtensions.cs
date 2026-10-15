namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Http上下文(<see cref="HttpContext"/>) 扩展
/// </summary>
public static partial class BingHttpContextExtensions
{
    /// <summary>
    /// 获取远程IP地址
    /// </summary>
    /// <param name="context">Http上下文</param>
    /// <returns>请求的远程 IP 地址；无法获取时返回 null。</returns>
    /// <remarks>默认只读取连接对端地址，不直接信任客户端提交的 <c>X-Forwarded-For</c>。代理场景应先配置 ASP.NET Core 的受信 <c>ForwardedHeaders</c> 中间件。</remarks>
    public static string GetRemoteIpAddress(this HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
#if DEBUG
        if (ip != null && ip.Contains("::ffff:"))
            return ip.Split("::ffff:")[1];
        if (ip == "::1" || ip?.Contains("127.0.0.1") == true)
            return "127.0.0.1";
#endif
        return ip;
    }
}
