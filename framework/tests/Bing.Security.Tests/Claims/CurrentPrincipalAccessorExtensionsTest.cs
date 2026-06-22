using System.Security.Claims;
using Bing.Security.Claims;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Claims;

/// <summary>
/// <see cref="CurrentPrincipalAccessorExtensions"/> 单元测试
/// </summary>
public class CurrentPrincipalAccessorExtensionsTest
{
    private readonly ThreadCurrentPrincipalAccessor _accessor = new();

    // ═══════════════════════════════════════════════════════════
    // Change(Claim)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Change(Claim) 应将单个 Claim 包装为 ClaimsPrincipal 后更新当前主体，
    /// 使 Principal.FindFirst 能找到该 Claim。
    /// </summary>
    [Fact]
    public void Change_WithSingleClaim_ShouldSetPrincipalWithThatClaim()
    {
        // Arrange
        var claim = new Claim(ClaimTypes.Name, "alice");

        // Act
        using (_accessor.Change(claim))
        {
            // Assert
            _accessor.Principal.ShouldNotBeNull();
            _accessor.Principal.FindFirst(ClaimTypes.Name)?.Value.ShouldBe("alice");
        }
    }

    /// <summary>
    /// 测试目的：Change(Claim) 返回的 IDisposable Dispose 后，当前主体应恢复为变更前的值。
    /// </summary>
    [Fact]
    public void Change_WithSingleClaim_AfterDispose_ShouldRestoreOriginalPrincipal()
    {
        // Arrange
        var originalPrincipal = _accessor.Principal;
        var claim = new Claim(ClaimTypes.Name, "alice");

        // Act
        var disposable = _accessor.Change(claim);
        disposable.Dispose();

        // Assert — 恢复原始主体
        _accessor.Principal.ShouldBeSameAs(originalPrincipal);
    }

    // ═══════════════════════════════════════════════════════════
    // Change(IEnumerable<Claim>)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Change(IEnumerable&lt;Claim&gt;) 应将多个 Claim 一起包装为 ClaimsPrincipal，
    /// 使 Principal 中能找到所有指定 Claim。
    /// </summary>
    [Fact]
    public void Change_WithMultipleClaims_ShouldSetPrincipalWithAllClaims()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "bob"),
            new Claim(ClaimTypes.Email, "bob@example.com")
        };

        // Act
        using (_accessor.Change(claims.AsEnumerable()))
        {
            // Assert
            _accessor.Principal.FindFirst(ClaimTypes.Name)?.Value.ShouldBe("bob");
            _accessor.Principal.FindFirst(ClaimTypes.Email)?.Value.ShouldBe("bob@example.com");
        }
    }

    /// <summary>
    /// 测试目的：Change(IEnumerable&lt;Claim&gt;) Dispose 后主体应恢复为变更前的值。
    /// </summary>
    [Fact]
    public void Change_WithMultipleClaims_AfterDispose_ShouldRestoreOriginalPrincipal()
    {
        // Arrange
        var originalPrincipal = _accessor.Principal;
        var claims = new[] { new Claim(ClaimTypes.Name, "bob") };

        // Act
        var disposable = _accessor.Change(claims.AsEnumerable());
        disposable.Dispose();

        // Assert
        _accessor.Principal.ShouldBeSameAs(originalPrincipal);
    }

    // ═══════════════════════════════════════════════════════════
    // Change(ClaimsIdentity)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Change(ClaimsIdentity) 应将给定身份包装为 ClaimsPrincipal，
    /// 主体的 AuthenticationType 应与传入 Identity 一致。
    /// </summary>
    [Fact]
    public void Change_WithClaimsIdentity_ShouldSetPrincipalWithThatIdentity()
    {
        // Arrange
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "carol") }, "custom-auth");

        // Act
        using (_accessor.Change(identity))
        {
            // Assert
            _accessor.Principal.Identity?.AuthenticationType.ShouldBe("custom-auth");
            _accessor.Principal.FindFirst(ClaimTypes.Name)?.Value.ShouldBe("carol");
        }
    }

    /// <summary>
    /// 测试目的：Change(ClaimsIdentity) Dispose 后主体应恢复为变更前的值。
    /// </summary>
    [Fact]
    public void Change_WithClaimsIdentity_AfterDispose_ShouldRestoreOriginalPrincipal()
    {
        // Arrange
        var originalPrincipal = _accessor.Principal;
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "carol") }, "test");

        // Act
        var disposable = _accessor.Change(identity);
        disposable.Dispose();

        // Assert
        _accessor.Principal.ShouldBeSameAs(originalPrincipal);
    }
}
