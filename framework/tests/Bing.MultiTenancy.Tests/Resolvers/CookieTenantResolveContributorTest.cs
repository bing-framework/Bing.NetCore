using Bing.AspNetCore.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading.Tasks;

namespace Bing.MultiTenancy.Resolvers;

/// <summary>
/// Cookie租户解析构造器测试
/// </summary>
public class CookieTenantResolveContributorTest
{
    /// <summary>
    /// 请求头租户解析构造器
    /// </summary>
    private readonly CookieTenantResolveContributor _resolver;

    /// <summary>
    /// 模拟服务提供程序访问器
    /// </summary>
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    /// <summary>
    /// 模拟Http上下文
    /// </summary>
    private readonly Mock<HttpContext> _mockHttpContext;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public CookieTenantResolveContributorTest()
    {
        _resolver = new CookieTenantResolveContributor();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockHttpContext = new Mock<HttpContext>();
    }

    /// <summary>
    /// 测试 - 解析租户标识 - 默认租户键名
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_DefaultKey()
    {
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置Cookie
        var mockCookie = new Mock<IRequestCookieCollection>();
        mockCookie
            .Setup(t => t[options.TenantKey])
            .Returns("a");
        _mockHttpContext.Setup(t => t.Request.Cookies).Returns(mockCookie.Object);
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IHttpContextAccessor)))
            .Returns(new HttpContextAccessor { HttpContext = _mockHttpContext.Object });
        
        // 执行
        var context = new TenantResolveContext(_mockServiceProvider.Object);
        await _resolver.ResolveAsync(context);

        // 验证
        Assert.Equal("a", context.TenantIdOrName);
    }

    /// <summary>
    /// 测试 - 解析租户标识 - 指定租户键名
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_CustomKey()
    {
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true, TenantKey = "key" };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置Cookie
        var mockCookie = new Mock<IRequestCookieCollection>();
        mockCookie
            .Setup(t => t[options.TenantKey])
            .Returns("a");
        _mockHttpContext.Setup(t => t.Request.Cookies).Returns(mockCookie.Object);
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IHttpContextAccessor)))
            .Returns(new HttpContextAccessor { HttpContext = _mockHttpContext.Object });
        
        // 执行
        var context = new TenantResolveContext(_mockServiceProvider.Object);
        await _resolver.ResolveAsync(context);

        // 验证
        Assert.Equal("a", context.TenantIdOrName);
    }
}
