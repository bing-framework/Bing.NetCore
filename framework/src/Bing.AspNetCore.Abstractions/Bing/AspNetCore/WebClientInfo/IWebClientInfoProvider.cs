using Bing.Aspects;

namespace Bing.AspNetCore.WebClientInfo;

/// <summary>
/// 提供 Web 客户端信息的接口
/// </summary>
[IgnoreAspect]
public interface IWebClientInfoProvider
{
    /// <summary>
    /// 获取客户端的浏览器信息。
    /// </summary>
    /// <remarks>
    /// 该信息通常包含浏览器的名称、版本、渲染引擎等，如： <br />
    /// - "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36" <br />
    /// - "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/537.36 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/537.36"
    /// </remarks>
    string? BrowserInfo { get; }

    /// <summary>
    /// 获取客户端的 IP 地址。
    /// </summary>
    /// <remarks>
    /// 该 IP 地址通常是用户访问服务器时的公网或内网 IP，例如： <br />
    /// - IPv4: "192.168.1.1" <br />
    /// - IPv6: "2001:db8::ff00:42:8329"
    /// </remarks>
    string? ClientIpAddress { get; }

    /// <summary>
    /// 获取客户端的设备信息。
    /// </summary>
    /// <remarks>
    /// 设备信息可以是操作系统、设备类型等，例如： <br />
    /// - "Windows 10, PC" <br />
    /// - "iPhone 13, iOS 16" <br />
    /// - "Samsung Galaxy S21, Android 13"
    /// </remarks>
    string? DeviceInfo { get; }
}
