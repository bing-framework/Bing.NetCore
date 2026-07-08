using System.Security.Claims;
using Bing.Test.Shared.Identity;
using Bing.Users;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Users;

/// <summary>
/// <see cref="FakeCurrentUser"/> 自身行为测试——
/// 确认测试基建（test double）的契约符合 <see cref="ICurrentUser"/> 预期。
/// </summary>
public class FakeCurrentUserTest
{
    // ── AsAnonymous ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：AsAnonymous() 创建的用户应处于未认证状态，所有标识字段为默认空值。
    /// </summary>
    [Fact]
    public void AsAnonymous_ShouldBeUnauthenticated()
    {
        // Arrange & Act
        var user = FakeCurrentUser.AsAnonymous();

        // Assert
        user.IsAuthenticated.ShouldBeFalse();
        user.UserId.ShouldBe(string.Empty);
        user.UserName.ShouldBe(string.Empty);
        user.TenantId.ShouldBe(string.Empty);
        user.Roles.ShouldBeEmpty();
    }

    // ── AsAuthenticated ───────────────────────────────────────────

    /// <summary>
    /// 测试目的：AsAuthenticated() 应正确填充 UserId / UserName / TenantId / Roles。
    /// </summary>
    [Fact]
    public void AsAuthenticated_WithAllParams_ShouldFillAllProperties()
    {
        // Arrange & Act
        var user = FakeCurrentUser.AsAuthenticated(
            userId: "u-001",
            userName: "alice",
            tenantId: "tenant-abc",
            roles: new[] { "Admin", "Editor" });

        // Assert
        user.IsAuthenticated.ShouldBeTrue();
        user.UserId.ShouldBe("u-001");
        user.UserName.ShouldBe("alice");
        user.TenantId.ShouldBe("tenant-abc");
        user.Roles.ShouldContain("Admin");
        user.Roles.ShouldContain("Editor");
    }

    /// <summary>
    /// 测试目的：AsAuthenticated() 在不传 tenantId 时，TenantId 应为空字符串（非 null）。
    /// </summary>
    [Fact]
    public void AsAuthenticated_WithoutTenantId_ShouldHaveEmptyTenantId()
    {
        // Arrange & Act
        var user = FakeCurrentUser.AsAuthenticated();

        // Assert
        user.TenantId.ShouldBe(string.Empty);
    }

    // ── WithClaim (链式调用) ───────────────────────────────────────

    /// <summary>
    /// 测试目的：WithClaim() 链式添加多个声明后，FindClaim / FindClaims 均应可查询。
    /// </summary>
    [Fact]
    public void WithClaim_Chained_ShouldAddMultipleClaims()
    {
        // Arrange & Act
        var user = FakeCurrentUser.AsAuthenticated()
            .WithClaim("custom_a", "value_a")
            .WithClaim("custom_b", "value_b");

        // Assert
        user.FindClaim("custom_a")?.Value.ShouldBe("value_a");
        user.FindClaim("custom_b")?.Value.ShouldBe("value_b");
    }

    /// <summary>
    /// 测试目的：同一 Claim 类型添加多次时，FindClaims() 应返回所有值，FindClaim() 返回第一个。
    /// </summary>
    [Fact]
    public void WithClaim_DuplicateType_ShouldReturnAllViaFindClaims()
    {
        // Arrange & Act
        var user = FakeCurrentUser.AsAuthenticated()
            .WithClaim("role", "Admin")
            .WithClaim("role", "Editor");

        // Assert
        var all = user.FindClaims("role");
        all.Length.ShouldBe(2);

        var first = user.FindClaim("role");
        first?.Value.ShouldBe("Admin");
    }

    // ── GetAllClaims ──────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetAllClaims() 应返回所有通过 WithClaim 添加的声明。
    /// </summary>
    [Fact]
    public void GetAllClaims_ShouldReturnAllAddedClaims()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated()
            .WithClaim("c1", "v1")
            .WithClaim("c2", "v2")
            .WithClaim("c3", "v3");

        // Act
        var all = user.GetAllClaims();

        // Assert
        all.Length.ShouldBe(3);
    }

    // ── IsInRole ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：IsInRole() 应不区分大小写地匹配角色名。
    /// </summary>
    [Fact]
    public void IsInRole_ShouldBeCaseInsensitive()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated(roles: new[] { "SuperAdmin" });

        // Assert
        user.IsInRole("superadmin").ShouldBeTrue();
        user.IsInRole("SUPERADMIN").ShouldBeTrue();
        user.IsInRole("SuperAdmin").ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Roles 为 null 时，IsInRole() 应返回 false 而不抛 NullReferenceException。
    /// </summary>
    [Fact]
    public void IsInRole_WhenRolesIsNull_ShouldReturnFalse()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();
        user.Roles = null;

        // Act & Assert
        Should.NotThrow(() => user.IsInRole("Admin").ShouldBeFalse());
    }

    // ── 边界 ──────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：FindClaim() 对不存在的 Claim 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void FindClaim_WhenClaimNotExist_ShouldReturnNull()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();

        // Assert
        user.FindClaim("nonexistent").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindClaims() 对不存在的 Claim 类型应返回空数组，不为 null。
    /// </summary>
    [Fact]
    public void FindClaims_WhenClaimNotExist_ShouldReturnEmptyArray()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();

        // Act
        var result = user.FindClaims("nonexistent");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
