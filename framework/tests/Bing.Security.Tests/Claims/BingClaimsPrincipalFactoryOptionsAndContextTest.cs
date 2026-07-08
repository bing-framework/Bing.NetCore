using System.Security.Claims;
using Bing.Security.Claims;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Claims;

/// <summary>
/// <see cref="BingClaimsPrincipalFactoryOptions"/> 及 <see cref="BingClaimsPrincipalContributorContext"/> 单元测试
/// </summary>
public class BingClaimsPrincipalFactoryOptionsAndContextTest
{
    // ═══════════════════════════════════════════════════════════
    // BingClaimsPrincipalFactoryOptions
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 Contributors 和 DynamicContributors 不为 null，且初始为空，
    /// 防止遍历时触发 NullReferenceException。
    /// </summary>
    [Fact]
    public void Options_Default_ContributorListsShouldBeEmptyAndNotNull()
    {
        // Arrange & Act
        var options = new BingClaimsPrincipalFactoryOptions();

        // Assert
        options.Contributors.ShouldNotBeNull();
        options.Contributors.Count.ShouldBe(0);
        options.DynamicContributors.ShouldNotBeNull();
        options.DynamicContributors.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：默认构造后 DynamicClaims 包含 UserName、Role、Email 等预定义字段，
    /// 确保动态 Claims 刷新覆盖关键身份字段。
    /// </summary>
    [Fact]
    public void Options_Default_DynamicClaimsShouldContainExpectedFields()
    {
        // Arrange & Act
        var options = new BingClaimsPrincipalFactoryOptions();

        // Assert
        options.DynamicClaims.ShouldNotBeNull();
        options.DynamicClaims.ShouldContain(BingClaimTypes.UserName);
        options.DynamicClaims.ShouldContain(BingClaimTypes.Name);
        options.DynamicClaims.ShouldContain(BingClaimTypes.SurName);
        options.DynamicClaims.ShouldContain(BingClaimTypes.Role);
        options.DynamicClaims.ShouldContain(BingClaimTypes.Email);
        options.DynamicClaims.ShouldContain(BingClaimTypes.PhoneNumber);
    }

    /// <summary>
    /// 测试目的：默认构造后 IsRemoteRefreshEnabled = true，RemoteRefreshUrl 不为空，
    /// 确保微服务身份同步开箱即用。
    /// </summary>
    [Fact]
    public void Options_Default_RemoteRefreshShouldBeEnabledWithDefaultUrl()
    {
        // Arrange & Act
        var options = new BingClaimsPrincipalFactoryOptions();

        // Assert
        options.IsRemoteRefreshEnabled.ShouldBeTrue();
        options.RemoteRefreshUrl.ShouldNotBeNullOrEmpty();
        options.RemoteRefreshUrl.ShouldBe("/api/account/dynamic-claims/refresh");
    }

    /// <summary>
    /// 测试目的：默认构造后 ClaimsMap 包含 UserName、Role、Email 的映射条目，
    /// 确保 OpenID Connect 标准声明能被正确转换。
    /// </summary>
    [Fact]
    public void Options_Default_ClaimsMapShouldContainDefaultMappings()
    {
        // Arrange & Act
        var options = new BingClaimsPrincipalFactoryOptions();

        // Assert
        options.ClaimsMap.ShouldNotBeNull();
        options.ClaimsMap.ContainsKey(BingClaimTypes.UserName).ShouldBeTrue();
        options.ClaimsMap.ContainsKey(BingClaimTypes.Role).ShouldBeTrue();
        options.ClaimsMap.ContainsKey(BingClaimTypes.Email).ShouldBeTrue();
        // UserName 映射来源至少包含 preferred_username
        options.ClaimsMap[BingClaimTypes.UserName].ShouldContain("preferred_username");
    }

    /// <summary>
    /// 测试目的：默认构造后 IsDynamicClaimsEnabled = false，
    /// 防止在未显式开启时误启用动态 Claims 计算导致性能损耗。
    /// </summary>
    [Fact]
    public void Options_Default_IsDynamicClaimsEnabledShouldBeFalse()
    {
        // Arrange & Act
        var options = new BingClaimsPrincipalFactoryOptions();

        // Assert
        options.IsDynamicClaimsEnabled.ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // BingClaimsPrincipalContributorContext
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：构造器应正确存储 ClaimsPrincipal 和 ServiceProvider，
    /// 确保贡献者能通过 context 访问依赖。
    /// </summary>
    [Fact]
    public void Context_Constructor_ShouldSetClaimsPrincipalAndServiceProvider()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var mockSp = new Mock<IServiceProvider>();

        // Act
        var context = new BingClaimsPrincipalContributorContext(principal, mockSp.Object);

        // Assert
        context.ClaimsPrincipal.ShouldBeSameAs(principal);
        context.ServiceProvider.ShouldBeSameAs(mockSp.Object);
    }

    /// <summary>
    /// 测试目的：ClaimsPrincipal 属性是可变的，贡献者可将修改后的主体写回 context。
    /// </summary>
    [Fact]
    public void Context_ClaimsPrincipal_ShouldBeMutable()
    {
        // Arrange
        var original = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var mockSp = new Mock<IServiceProvider>();
        var context = new BingClaimsPrincipalContributorContext(original, mockSp.Object);
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity("updated"));

        // Act
        context.ClaimsPrincipal = newPrincipal;

        // Assert
        context.ClaimsPrincipal.ShouldBeSameAs(newPrincipal);
    }

    /// <summary>
    /// 测试目的：GetRequiredService&lt;T&gt;() 应从 ServiceProvider 正确解析服务，
    /// 确保贡献者能按需获取依赖实例。
    /// </summary>
    [Fact]
    public void Context_GetRequiredService_ShouldResolveFromServiceProvider()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var mockSp = new Mock<IServiceProvider>();
        var expectedAccessor = new ThreadCurrentPrincipalAccessor();
        mockSp
            .Setup(sp => sp.GetService(typeof(ICurrentPrincipalAccessor)))
            .Returns(expectedAccessor);
        var context = new BingClaimsPrincipalContributorContext(principal, mockSp.Object);

        // Act
        var resolved = context.GetRequiredService<ICurrentPrincipalAccessor>();

        // Assert
        resolved.ShouldNotBeNull();
        resolved.ShouldBeSameAs(expectedAccessor);
    }
}
