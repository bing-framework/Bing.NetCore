using Moq;
using System.Threading.Tasks;
using System;
using Bing.Users;
using Microsoft.Extensions.Options;

namespace Bing.MultiTenancy.Resolvers;

/// <summary>
/// 当前用户租户解析构造器测试
/// </summary>
public class CurrentUserTenantResolveContributorTest
{
    /// <summary>
    /// 请求头租户解析构造器
    /// </summary>
    private readonly CurrentUserTenantResolveContributor _resolver;

    /// <summary>
    /// 模拟服务提供程序访问器
    /// </summary>
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    /// <summary>
    /// 模拟当前用户
    /// </summary>
    private readonly Mock<ICurrentUser> _mockCurrentUser;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public CurrentUserTenantResolveContributorTest()
    {
        _resolver = new CurrentUserTenantResolveContributor();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockCurrentUser = new Mock<ICurrentUser>();
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

        // 设置当前用户
        _mockCurrentUser.Setup(x => x.TenantId)
            .Returns("a");
        _mockCurrentUser.Setup(x => x.IsAuthenticated)
            .Returns(true);
        _mockServiceProvider
            .Setup(x => x.GetService(typeof(ICurrentUser)))
            .Returns(_mockCurrentUser.Object);
        
        // 执行
        var context = new TenantResolveContext(_mockServiceProvider.Object);
        await _resolver.ResolveAsync(context);

        // 验证
        Assert.Equal("a", context.TenantIdOrName);
    }
}
