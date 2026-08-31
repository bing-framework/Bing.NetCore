using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Action上下文(<see cref="ActionContext"/>) 扩展
/// </summary>
public static partial class BingActionContextExtensions
{
    /// <summary>
    /// 获取远程IP地址
    /// </summary>
    /// <param name="context">Action上下文</param>
    /// <returns>请求的远程 IP 地址；无法获取时返回 null。</returns>
    public static string GetRemoteIpAddress(this ActionContext context) => context.HttpContext.GetRemoteIpAddress();
}