using System.Security.Claims;
using Bing.Security.Principals;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Principals;

/// <summary>
/// <see cref="UnauthenticatedIdentity"/> 及 <see cref="UnauthenticatedPrincipal"/> 单元测试
/// </summary>
public class UnauthenticatedTest
{
    // ═══════════════════════════════════════════════════════════
    // UnauthenticatedIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：UnauthenticatedIdentity.IsAuthenticated 始终为 false，表示未认证身份。
    /// </summary>
    [Fact]
    public void UnauthenticatedIdentity_IsAuthenticated_ShouldAlwaysBeFalse()
    {
        // Arrange & Act
        var identity = new UnauthenticatedIdentity();

        // Assert
        identity.IsAuthenticated.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：UnauthenticatedIdentity.Instance 是单例，避免重复实例化的开销。
    /// </summary>
    [Fact]
    public void UnauthenticatedIdentity_Instance_ShouldBeSingleton()
    {
        // Arrange & Act
        var a = UnauthenticatedIdentity.Instance;
        var b = UnauthenticatedIdentity.Instance;

        // Assert
        a.ShouldNotBeNull();
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：UnauthenticatedIdentity 应继承自 ClaimsIdentity，兼容 ClaimsPrincipal 体系。
    /// </summary>
    [Fact]
    public void UnauthenticatedIdentity_ShouldInheritFromClaimsIdentity()
    {
        // Arrange & Act
        var identity = UnauthenticatedIdentity.Instance;

        // Assert
        identity.ShouldBeAssignableTo<ClaimsIdentity>();
    }

    /// <summary>
    /// 测试目的：Instance 的 IsAuthenticated 应为 false，与直接构造一致。
    /// </summary>
    [Fact]
    public void UnauthenticatedIdentity_Instance_IsAuthenticated_ShouldBeFalse()
    {
        // Arrange & Act & Assert
        UnauthenticatedIdentity.Instance.IsAuthenticated.ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // UnauthenticatedPrincipal
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：UnauthenticatedPrincipal.Instance 是单例，避免重复实例化的开销。
    /// </summary>
    [Fact]
    public void UnauthenticatedPrincipal_Instance_ShouldBeSingleton()
    {
        // Arrange & Act
        var a = UnauthenticatedPrincipal.Instance;
        var b = UnauthenticatedPrincipal.Instance;

        // Assert
        a.ShouldNotBeNull();
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：UnauthenticatedPrincipal.Identity 应返回 UnauthenticatedIdentity.Instance，实现对象复用。
    /// </summary>
    [Fact]
    public void UnauthenticatedPrincipal_Identity_ShouldBeUnauthenticatedIdentityInstance()
    {
        // Arrange & Act
        var principal = UnauthenticatedPrincipal.Instance;

        // Assert
        ReferenceEquals(principal.Identity, UnauthenticatedIdentity.Instance).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：UnauthenticatedPrincipal.Identity.IsAuthenticated 应为 false（通过主体对象访问）。
    /// </summary>
    [Fact]
    public void UnauthenticatedPrincipal_Identity_IsAuthenticated_ShouldBeFalse()
    {
        // Arrange & Act
        var principal = UnauthenticatedPrincipal.Instance;

        // Assert
        principal.Identity.IsAuthenticated.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：UnauthenticatedPrincipal 应继承自 ClaimsPrincipal，与 ASP.NET Core 主体体系兼容。
    /// </summary>
    [Fact]
    public void UnauthenticatedPrincipal_ShouldInheritFromClaimsPrincipal()
    {
        // Arrange & Act
        var principal = UnauthenticatedPrincipal.Instance;

        // Assert
        principal.ShouldBeAssignableTo<ClaimsPrincipal>();
    }
}
