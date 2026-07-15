using System.Security.Claims;
using Bing.Security.Claims;
using Bing.Test.Shared.Identity;
using Bing.Users;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Users;

/// <summary>
/// <see cref="CurrentUserExtensions"/> 单元测试
/// 所有测试使用 <see cref="FakeCurrentUser"/> 替代真实 HttpContext/Principal，
/// 确保测试无外部依赖、快速、可重复。
/// </summary>
public class CurrentUserExtensionsTest
{
    // ── GetUserId ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetUserId() 应将 UserId 字符串解析为 Guid。
    /// </summary>
    [Fact]
    public void GetUserId_WithValidGuidString_ShouldReturnGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var user = FakeCurrentUser.AsAuthenticated(userId: guid.ToString());

        // Act
        var result = user.GetUserId();

        // Assert
        result.ShouldBe(guid);
    }

    /// <summary>
    /// 测试目的：GetUserId{T}() 应将 UserId 转换为 int。
    /// </summary>
    [Fact]
    public void GetUserId_WithIntType_ShouldReturnInt()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated(userId: "99");

        // Act
        var result = user.GetUserId<int>();

        // Assert
        result.ShouldBe(99);
    }

    // ── GetUserName (Claim-based) ─────────────────────────────────

    /// <summary>
    /// 测试目的：GetUserName() 应从 BingClaimTypes.UserName Claim 中读取用户名。
    /// </summary>
    [Fact]
    public void GetUserName_WithUserNameClaim_ShouldReturnClaimValue()
    {
        // Arrange
        var user = new FakeCurrentUser()
            .WithClaim(BingClaimTypes.UserName, "testuser");

        // Act
        var result = user.GetUserName();

        // Assert
        result.ShouldBe("testuser");
    }

    /// <summary>
    /// 测试目的：GetUserName() 在 BingClaimTypes.UserName 缺失时应回退到 "name" Claim。
    /// </summary>
    [Fact]
    public void GetUserName_WhenUserNameClaimMissing_ShouldFallbackToNameClaim()
    {
        // Arrange
        var user = new FakeCurrentUser().WithClaim("name", "fallback-user");

        // Act
        var result = user.GetUserName();

        // Assert
        result.ShouldBe("fallback-user");
    }

    /// <summary>
    /// 测试目的：GetUserName() 在无任何相关 Claim 时应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void GetUserName_WhenNoClaim_ShouldReturnNull()
    {
        // Arrange
        var user = FakeCurrentUser.AsAnonymous();

        // Act
        var result = user.GetUserName();

        // Assert
        result.ShouldBeNullOrWhiteSpace();
    }

    // ── GetFullName ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetFullName() 应从 BingClaimTypes.FullName Claim 中读取全名。
    /// </summary>
    [Fact]
    public void GetFullName_WithFullNameClaim_ShouldReturnClaimValue()
    {
        // Arrange
        var user = new FakeCurrentUser().WithClaim(BingClaimTypes.FullName, "张 三");

        // Act
        var result = user.GetFullName();

        // Assert
        result.ShouldBe("张 三");
    }

    /// <summary>
    /// 测试目的：GetFullName() 在缺失主 Claim 时应回退到 "family_name" Claim。
    /// </summary>
    [Fact]
    public void GetFullName_WhenFullNameClaimMissing_ShouldFallbackToFamilyName()
    {
        // Arrange
        var user = new FakeCurrentUser().WithClaim("family_name", "王");

        // Act
        var result = user.GetFullName();

        // Assert
        result.ShouldBe("王");
    }

    // ── FindClaimValue ────────────────────────────────────────────

    /// <summary>
    /// 测试目的：FindClaimValue() 对存在的 Claim 应返回其 Value。
    /// </summary>
    [Fact]
    public void FindClaimValue_WhenClaimExists_ShouldReturnValue()
    {
        // Arrange
        var user = new FakeCurrentUser().WithClaim("custom_type", "custom_value");

        // Act
        var result = user.FindClaimValue("custom_type");

        // Assert
        result.ShouldBe("custom_value");
    }

    /// <summary>
    /// 测试目的：FindClaimValue() 对不存在的 Claim 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void FindClaimValue_WhenClaimNotExist_ShouldReturnNull()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();

        // Act
        var result = user.FindClaimValue("nonexistent_type");

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindClaimValue{T}() 对存在的 Claim 应将值转换为指定类型。
    /// </summary>
    [Fact]
    public void FindClaimValueGeneric_WhenClaimExists_ShouldConvertToType()
    {
        // Arrange
        var user = new FakeCurrentUser().WithClaim("age", "30");

        // Act
        var result = user.FindClaimValue<int>("age");

        // Assert
        result.ShouldBe(30);
    }

    // ── GetTenantId ───────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetTenantId() 应从 BingClaimTypes.TenantId Claim 解析 Guid。
    /// </summary>
    [Fact]
    public void GetTenantId_WithTenantIdClaim_ShouldReturnGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = new FakeCurrentUser()
            .WithClaim(BingClaimTypes.TenantId, tenantId.ToString());

        // Act
        var result = user.GetTenantId();

        // Assert
        result.ShouldBe(tenantId);
    }

    /// <summary>
    /// 测试目的：GetTenantId() 在无租户 Claim 时应返回 Guid.Empty，不抛异常。
    /// </summary>
    [Fact]
    public void GetTenantId_WhenNoTenantClaim_ShouldReturnGuidEmpty()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();

        // Act
        var result = user.GetTenantId();

        // Assert
        result.ShouldBe(Guid.Empty);
    }

    // ── IsInRole ──────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：IsInRole() 对已分配的角色应返回 true（不区分大小写）。
    /// </summary>
    [Fact]
    public void IsInRole_WithMatchingRole_ShouldReturnTrue()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated(roles: new[] { "Admin", "Editor" });

        // Act & Assert
        user.IsInRole("admin").ShouldBeTrue();
        user.IsInRole("EDITOR").ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsInRole() 对未分配的角色应返回 false。
    /// </summary>
    [Fact]
    public void IsInRole_WithUnassignedRole_ShouldReturnFalse()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated(roles: new[] { "User" });

        // Act & Assert
        user.IsInRole("Admin").ShouldBeFalse();
    }

    // ── FindImpersonatorTenantId ───────────────────────────────────

    /// <summary>
    /// 测试目的：FindImpersonatorTenantId() 在有模拟租户 Claim 时应正确解析 Guid。
    /// </summary>
    [Fact]
    public void FindImpersonatorTenantId_WithValidClaim_ShouldReturnGuid()
    {
        // Arrange
        var impersonatorId = Guid.NewGuid();
        var user = new FakeCurrentUser()
            .WithClaim(BingClaimTypes.ImpersonatorTenantId, impersonatorId.ToString());

        // Act
        var result = user.FindImpersonatorTenantId();

        // Assert
        result.ShouldBe(impersonatorId);
    }

    /// <summary>
    /// 测试目的：FindImpersonatorTenantId() 在无相关 Claim 时应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void FindImpersonatorTenantId_WhenNoClaim_ShouldReturnNull()
    {
        // Arrange
        var user = FakeCurrentUser.AsAuthenticated();

        // Act
        var result = user.FindImpersonatorTenantId();

        // Assert
        result.ShouldBeNull();
    }

    // ── GetRoleIds ────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：GetRoleIds() 应从 role_ids Claim 解析出 Guid 列表。
    /// </summary>
    [Fact]
    public void GetRoleIds_WithRoleIdsClaim_ShouldReturnGuidList()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        // BingClaimTypes.RoleIds 存储为逗号分隔的 Guid 字符串
        var user = new FakeCurrentUser()
            .WithClaim(BingClaimTypes.RoleIds, $"{id1},{id2}");

        // Act
        var result = user.GetRoleIds();

        // Assert
        result.ShouldContain(id1);
        result.ShouldContain(id2);
    }
}
