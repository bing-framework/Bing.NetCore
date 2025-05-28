using Moq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Options;

namespace Bing.MultiTenancy.Resolvers;

/// <summary>
/// Action租户解析构造器测试
/// </summary>
public class ActionTenantResolveContributorTest
{
    /// <summary>
    /// 请求头租户解析构造器
    /// </summary>
    private readonly ActionTenantResolveContributor _resolver;

    /// <summary>
    /// 模拟服务提供程序访问器
    /// </summary>
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ActionTenantResolveContributorTest()
    {
        _resolver = new ActionTenantResolveContributor(x =>
        {
            x.Handled = true;
            x.TenantIdOrName = "a";
        });
        _mockServiceProvider = new Mock<IServiceProvider>();
    }

    /// <summary>
    /// 测试 - 解析租户标识
    /// </summary>
    [Fact]
    public async Task Test_ResolveAsync_1()
    {
        // 设置租户配置
        var options = new MultiTenancyOptions { IsEnabled = true };
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(IOptions<MultiTenancyOptions>)))
            .Returns(Microsoft.Extensions.Options.Options.Create(options));

        // 执行
        var context = new TenantResolveContext(_mockServiceProvider.Object);
        await _resolver.ResolveAsync(context);

        // 验证
        Assert.Equal("a", context.TenantIdOrName);
    }
}
