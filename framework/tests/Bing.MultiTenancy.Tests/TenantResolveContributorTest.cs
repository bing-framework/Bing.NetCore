using Bing.Users;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.MultiTenancy;

/// <summary>
/// <see cref="ActionTenantResolveContributor"/> 和
/// <see cref="CurrentUserTenantResolveContributor"/> 单元测试
/// </summary>
public class TenantResolveContributorTest
{
    // ═══════════════════════════════════════════════════════════
    // ActionTenantResolveContributor — 直接测试
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入 null Action 时，构造器应抛出 ArgumentNullException，
    /// 防止 ResolveAsync 时出现 NRE。
    /// </summary>
    [Fact]
    public void ActionTenantResolveContributor_NullAction_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ActionTenantResolveContributor(null));
    }

    /// <summary>
    /// 测试目的：Name 属性应返回 "Action"（ContributorName 常量），
    /// 确保 TenantResolver 中的 AppliedResolvers 能正确识别此构造器。
    /// </summary>
    [Fact]
    public void ActionTenantResolveContributor_Name_ShouldBeAction()
    {
        // Arrange
        var contributor = new ActionTenantResolveContributor(_ => { });

        // Assert
        contributor.Name.ShouldBe(ActionTenantResolveContributor.ContributorName);
        contributor.Name.ShouldBe("Action");
    }

    /// <summary>
    /// 测试目的：ResolveAsync 应调用注入的 Action 并传入上下文，
    /// 确保自定义解析逻辑可以正确设置 TenantIdOrName。
    /// </summary>
    [Fact]
    public async Task ActionTenantResolveContributor_ResolveAsync_ShouldInvokeAction()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var ctx = new TenantResolveContext(mockSp.Object);
        var called = false;
        var contributor = new ActionTenantResolveContributor(c =>
        {
            called = true;
            c.TenantIdOrName = "action-tenant";
            c.Handled = true;
        });

        // Act
        await contributor.ResolveAsync(ctx);

        // Assert
        called.ShouldBeTrue();
        ctx.TenantIdOrName.ShouldBe("action-tenant");
        ctx.Handled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：ResolveAsync 应返回已完成的 Task（非 null），
    /// 确保调用方可以安全 await。
    /// </summary>
    [Fact]
    public async Task ActionTenantResolveContributor_ResolveAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var mockSp = new Mock<IServiceProvider>();
        var ctx = new TenantResolveContext(mockSp.Object);
        var contributor = new ActionTenantResolveContributor(_ => { });

        // Act & Assert（不抛异常即为通过）
        await contributor.ResolveAsync(ctx);
    }

    // ═══════════════════════════════════════════════════════════
    // CurrentUserTenantResolveContributor — Mock ICurrentUser
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 构建包含 Mock ICurrentUser 的 IServiceProvider
    /// </summary>
    private static IServiceProvider BuildServiceProvider(bool isAuthenticated, string? tenantId)
    {
        var mockUser = new Mock<ICurrentUser>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(isAuthenticated);
        mockUser.Setup(u => u.TenantId).Returns(tenantId);
        var services = new ServiceCollection();
        services.AddSingleton(mockUser.Object);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 测试目的：Name 属性应返回 "CurrentUser"（ContributorName 常量）。
    /// </summary>
    [Fact]
    public void CurrentUserTenantResolveContributor_Name_ShouldBeCurrentUser()
    {
        // Arrange
        var contributor = new CurrentUserTenantResolveContributor();

        // Assert
        contributor.Name.ShouldBe(CurrentUserTenantResolveContributor.ContributorName);
        contributor.Name.ShouldBe("CurrentUser");
    }

    /// <summary>
    /// 测试目的：用户已认证且有 TenantId 时，应将 Handled=true 并设置 TenantIdOrName，
    /// 确保租户解析链可以短路后续构造器。
    /// </summary>
    [Fact]
    public async Task CurrentUserTenantResolveContributor_AuthenticatedWithTenantId_ShouldSetHandled()
    {
        // Arrange
        var sp = BuildServiceProvider(isAuthenticated: true, tenantId: "tenant-42");
        var ctx = new TenantResolveContext(sp);
        var contributor = new CurrentUserTenantResolveContributor();

        // Act
        await contributor.ResolveAsync(ctx);

        // Assert
        ctx.Handled.ShouldBeTrue();
        ctx.TenantIdOrName.ShouldBe("tenant-42");
    }

    /// <summary>
    /// 测试目的：用户未认证时，Handled 应保持 false，不设置 TenantIdOrName，
    /// 确保匿名用户不会影响租户解析结果。
    /// </summary>
    [Fact]
    public async Task CurrentUserTenantResolveContributor_NotAuthenticated_ShouldNotSetHandled()
    {
        // Arrange
        var sp = BuildServiceProvider(isAuthenticated: false, tenantId: null);
        var ctx = new TenantResolveContext(sp);
        var contributor = new CurrentUserTenantResolveContributor();

        // Act
        await contributor.ResolveAsync(ctx);

        // Assert
        ctx.Handled.ShouldBeFalse();
        ctx.TenantIdOrName.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：用户已认证但 TenantId 为 null（宿主管理员场景），
    /// Handled 应为 true，TenantIdOrName 应为 null，
    /// 确保宿主身份可以正确终止租户解析链。
    /// </summary>
    [Fact]
    public async Task CurrentUserTenantResolveContributor_AuthenticatedWithNullTenantId_ShouldHandleAsHost()
    {
        // Arrange
        var sp = BuildServiceProvider(isAuthenticated: true, tenantId: null);
        var ctx = new TenantResolveContext(sp);
        var contributor = new CurrentUserTenantResolveContributor();

        // Act
        await contributor.ResolveAsync(ctx);

        // Assert
        ctx.Handled.ShouldBeTrue();
        ctx.TenantIdOrName.ShouldBeNull();
    }
}
