using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bing.AspNetCore.Mvc.Tests.Bing.AspNetCore.Http;

/// <summary>
/// 验证 HTTP 客户端地址读取的信任边界。
/// </summary>
public sealed class BingHttpContextExtensionsTest
{
    /// <summary>
    /// 客户端提交转发头时仍应使用连接对端地址。
    /// </summary>
    [Fact]
    public void GetRemoteIpAddress_WhenForwardedHeaderIsUntrusted_ShouldUseConnectionAddress()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";

        var address = context.GetRemoteIpAddress();

        Assert.Equal("10.0.0.5", address);
    }

    /// <summary>
    /// 没有连接对端地址时返回空值。
    /// </summary>
    [Fact]
    public void GetRemoteIpAddress_WhenConnectionAddressIsMissing_ShouldReturnNull()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.10";

        Assert.Null(context.GetRemoteIpAddress());
    }
}
