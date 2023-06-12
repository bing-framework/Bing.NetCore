using Bing.Aspects;

namespace Bing.AspNetCore.WebClientInfo;

/// <summary>
/// Web客户端信息提供程序
/// </summary>
[IgnoreAspect]
public interface IWebClientInfoProvider
{
    /// <summary>
    /// 浏览器信息
    /// </summary>
    string BrowserInfo { get; }

    /// <summary>
    /// 客户端IP地址
    /// </summary>
    string ClientIpAddress { get; }
}
