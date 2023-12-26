using Bing.AspNetCore.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Options;

namespace Bing.MultiTenancy.Resolvers;

/// <summary>
/// 域名租户解析构造器测试
/// </summary>
public class DomainTenantResolveContributorTest
{
    /// <summary>
    /// 请求头租户解析构造器
    /// </summary>
    private DomainTenantResolveContributor _resolver;

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
    public DomainTenantResolveContributorTest()
    {
        _resolver = new DomainTenantResolveContributor("{0}.test.com");
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockHttpContext = new Mock<HttpContext>();
    }

    /// <summary>
    /// 测试 - 解析租户标识 - 2级域名
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_1()
    {
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置域名
        _mockHttpContext.Setup(t => t.Request.Host).Returns(new HostString("http://a.test.com"));
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
    /// 测试 - 解析租户标识 - 顶级域名
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_2()
    {
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置域名
        _mockHttpContext.Setup(t => t.Request.Host).Returns(new HostString("http://test.com"));
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IHttpContextAccessor)))
            .Returns(new HttpContextAccessor { HttpContext = _mockHttpContext.Object });
        
        // 执行
        var context = new TenantResolveContext(_mockServiceProvider.Object);
        await _resolver.ResolveAsync(context);

        // 验证
        Assert.Null(context.TenantIdOrName);
    }

    /// <summary>
    /// 测试 - 解析租户标识 - 3级域名
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_3()
    {
        _resolver = new DomainTenantResolveContributor("{0}.b.test.com");
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置域名
        _mockHttpContext.Setup(t => t.Request.Host).Returns(new HostString("https://a.b.test.com"));
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
    /// 测试 - 解析租户标识 - 指定域名格式
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_4()
    {
        _resolver = new DomainTenantResolveContributor("b.{0}.test.com");
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 设置域名
        _mockHttpContext.Setup(t => t.Request.Host).Returns(new HostString("https://b.a.test.com"));
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
