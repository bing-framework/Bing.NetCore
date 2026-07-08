using System.Security.Claims;
using System.Security.Principal;
using Bing.Security.Claims;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Principal;

/// <summary>
/// <see cref="BingClaimsIdentityExtensions"/> 单元测试。
/// 覆盖所有扩展方法（ClaimsPrincipal 版本 + IIdentity 版本 + ClaimsIdentity 操作方法）。
/// </summary>
public class BingClaimsIdentityExtensionsTest
{
    #region 辅助方法

    /// <summary>
    /// 使用给定的声明集合构建 ClaimsPrincipal
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    /// <summary>
    /// 使用给定的声明集合构建 ClaimsIdentity
    /// </summary>
    private static ClaimsIdentity BuildIdentity(params Claim[] claims) =>
        new(claims, "TestAuth");

    #endregion

    // ═══════════════════════════════════════════════════════════
    // FindUserId — ClaimsPrincipal
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindUserId(ClaimsPrincipal) 在存在有效 Guid 格式的 UserId 声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindUserId_Principal_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var principal = BuildPrincipal(new Claim(BingClaimTypes.UserId, userId.ToString()));

        // Act
        var result = principal.FindUserId();

        // Assert
        result.ShouldBe(userId);
    }

    /// <summary>
    /// 测试目的：FindUserId(ClaimsPrincipal) 在无 UserId 声明时，应返回 null 而不抛异常。
    /// </summary>
    [Fact]
    public void FindUserId_Principal_WhenNoUserIdClaim_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindUserId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindUserId(ClaimsPrincipal) 在声明值不是合法 Guid 时，应返回 null。
    /// </summary>
    [Fact]
    public void FindUserId_Principal_WhenNonGuidValue_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal(new Claim(BingClaimTypes.UserId, "not-a-guid"));

        // Act
        var result = principal.FindUserId();

        // Assert
        result.ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // FindUserId — IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindUserId(IIdentity) 在存在有效声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindUserId_Identity_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.UserId, userId.ToString()));

        // Act
        var result = identity.FindUserId();

        // Assert
        result.ShouldBe(userId);
    }

    /// <summary>
    /// 测试目的：FindUserId(IIdentity) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindUserId_Identity_WhenNoUserIdClaim_ShouldReturnNull()
    {
        // Arrange
        IIdentity identity = BuildIdentity();

        // Act
        var result = identity.FindUserId();

        // Assert
        result.ShouldBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // FindTenantId — ClaimsPrincipal / IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindTenantId(ClaimsPrincipal) 在存在有效 TenantId 声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindTenantId_Principal_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var principal = BuildPrincipal(new Claim(BingClaimTypes.TenantId, tenantId.ToString()));

        // Act
        var result = principal.FindTenantId();

        // Assert
        result.ShouldBe(tenantId);
    }

    /// <summary>
    /// 测试目的：FindTenantId(ClaimsPrincipal) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindTenantId_Principal_WhenNoTenantIdClaim_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindTenantId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindTenantId(IIdentity) 在存在有效 TenantId 声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindTenantId_Identity_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.TenantId, tenantId.ToString()));

        // Act
        var result = identity.FindTenantId();

        // Assert
        result.ShouldBe(tenantId);
    }

    // ═══════════════════════════════════════════════════════════
    // FindClientId — ClaimsPrincipal / IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindClientId(ClaimsPrincipal) 在存在 ClientId 声明时，应返回对应字符串。
    /// </summary>
    [Fact]
    public void FindClientId_Principal_WhenClientIdClaimExists_ShouldReturnString()
    {
        // Arrange
        var principal = BuildPrincipal(new Claim(BingClaimTypes.ClientId, "client-001"));

        // Act
        var result = principal.FindClientId();

        // Assert
        result.ShouldBe("client-001");
    }

    /// <summary>
    /// 测试目的：FindClientId(ClaimsPrincipal) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindClientId_Principal_WhenNoClientIdClaim_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindClientId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindClientId(IIdentity) 在存在 ClientId 声明时，应返回对应字符串。
    /// </summary>
    [Fact]
    public void FindClientId_Identity_WhenClientIdClaimExists_ShouldReturnString()
    {
        // Arrange
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.ClientId, "client-002"));

        // Act
        var result = identity.FindClientId();

        // Assert
        result.ShouldBe("client-002");
    }

    // ═══════════════════════════════════════════════════════════
    // FindEditionId — ClaimsPrincipal / IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindEditionId(ClaimsPrincipal) 在存在有效 EditionId 声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindEditionId_Principal_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        var principal = BuildPrincipal(new Claim(BingClaimTypes.EditionId, editionId.ToString()));

        // Act
        var result = principal.FindEditionId();

        // Assert
        result.ShouldBe(editionId);
    }

    /// <summary>
    /// 测试目的：FindEditionId(ClaimsPrincipal) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindEditionId_Principal_WhenNoEditionIdClaim_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindEditionId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindEditionId(IIdentity) 在存在有效 EditionId 声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindEditionId_Identity_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var editionId = Guid.NewGuid();
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.EditionId, editionId.ToString()));

        // Act
        var result = identity.FindEditionId();

        // Assert
        result.ShouldBe(editionId);
    }

    // ═══════════════════════════════════════════════════════════
    // FindImpersonatorTenantId — ClaimsPrincipal / IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindImpersonatorTenantId(ClaimsPrincipal) 在存在有效声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindImpersonatorTenantId_Principal_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var impersonatorTenantId = Guid.NewGuid();
        var principal = BuildPrincipal(new Claim(BingClaimTypes.ImpersonatorTenantId, impersonatorTenantId.ToString()));

        // Act
        var result = principal.FindImpersonatorTenantId();

        // Assert
        result.ShouldBe(impersonatorTenantId);
    }

    /// <summary>
    /// 测试目的：FindImpersonatorTenantId(ClaimsPrincipal) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindImpersonatorTenantId_Principal_WhenNoClaimExists_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindImpersonatorTenantId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindImpersonatorTenantId(IIdentity) 在存在有效声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindImpersonatorTenantId_Identity_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var id = Guid.NewGuid();
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.ImpersonatorTenantId, id.ToString()));

        // Act
        var result = identity.FindImpersonatorTenantId();

        // Assert
        result.ShouldBe(id);
    }

    // ═══════════════════════════════════════════════════════════
    // FindImpersonatorUserId — ClaimsPrincipal / IIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindImpersonatorUserId(ClaimsPrincipal) 在存在有效声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindImpersonatorUserId_Principal_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var impersonatorUserId = Guid.NewGuid();
        var principal = BuildPrincipal(new Claim(BingClaimTypes.ImpersonatorUserId, impersonatorUserId.ToString()));

        // Act
        var result = principal.FindImpersonatorUserId();

        // Assert
        result.ShouldBe(impersonatorUserId);
    }

    /// <summary>
    /// 测试目的：FindImpersonatorUserId(ClaimsPrincipal) 在无声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindImpersonatorUserId_Principal_WhenNoClaimExists_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindImpersonatorUserId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindImpersonatorUserId(IIdentity) 在存在有效声明时，应返回对应 Guid。
    /// </summary>
    [Fact]
    public void FindImpersonatorUserId_Identity_WhenValidGuidClaim_ShouldReturnGuid()
    {
        // Arrange
        var id = Guid.NewGuid();
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.ImpersonatorUserId, id.ToString()));

        // Act
        var result = identity.FindImpersonatorUserId();

        // Assert
        result.ShouldBe(id);
    }

    // ═══════════════════════════════════════════════════════════
    // FindSessionId — ClaimsPrincipal / IIdentity
    // 注意：实现上复用 ClientId 声明类型（非 SessionId）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：FindSessionId(ClaimsPrincipal) 在存在 ClientId 声明（实现层复用）时，应返回对应字符串。
    /// </summary>
    [Fact]
    public void FindSessionId_Principal_WhenClientIdClaimExists_ShouldReturnString()
    {
        // Arrange
        var principal = BuildPrincipal(new Claim(BingClaimTypes.ClientId, "session-abc"));

        // Act
        var result = principal.FindSessionId();

        // Assert
        result.ShouldBe("session-abc");
    }

    /// <summary>
    /// 测试目的：FindSessionId(ClaimsPrincipal) 在无任何相关声明时，应返回 null。
    /// </summary>
    [Fact]
    public void FindSessionId_Principal_WhenNoClientIdClaim_ShouldReturnNull()
    {
        // Arrange
        var principal = BuildPrincipal();

        // Act
        var result = principal.FindSessionId();

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindSessionId(IIdentity) 在存在 ClientId 声明（实现层复用）时，应返回对应字符串。
    /// </summary>
    [Fact]
    public void FindSessionId_Identity_WhenClientIdClaimExists_ShouldReturnString()
    {
        // Arrange
        IIdentity identity = BuildIdentity(new Claim(BingClaimTypes.ClientId, "session-xyz"));

        // Act
        var result = identity.FindSessionId();

        // Assert
        result.ShouldBe("session-xyz");
    }

    // ═══════════════════════════════════════════════════════════
    // AddIfNotContains — ClaimsIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddIfNotContains 在声明不存在时，应将该声明添加到标识中。
    /// </summary>
    [Fact]
    public void AddIfNotContains_WhenClaimNotExists_ShouldAddClaim()
    {
        // Arrange
        var identity = BuildIdentity();
        var claim = new Claim("custom_type", "custom_value");

        // Act
        identity.AddIfNotContains(claim);

        // Assert
        identity.FindFirst("custom_type").ShouldNotBeNull();
        identity.FindFirst("custom_type")!.Value.ShouldBe("custom_value");
    }

    /// <summary>
    /// 测试目的：AddIfNotContains 在声明已存在时，不应添加重复声明，且返回值为原标识（链式调用）。
    /// </summary>
    [Fact]
    public void AddIfNotContains_WhenClaimAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var identity = BuildIdentity(new Claim("dup_type", "original"));
        var duplicate = new Claim("dup_type", "new_value");

        // Act
        var returned = identity.AddIfNotContains(duplicate);

        // Assert
        // 仍然只有一条声明
        identity.FindAll("dup_type").Count().ShouldBe(1);
        // 保留原始值
        identity.FindFirst("dup_type")!.Value.ShouldBe("original");
        // 返回自身（链式调用）
        returned.ShouldBeSameAs(identity);
    }

    // ═══════════════════════════════════════════════════════════
    // AddOrReplace — ClaimsIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddOrReplace 在声明不存在时，应直接添加。
    /// </summary>
    [Fact]
    public void AddOrReplace_WhenClaimNotExists_ShouldAddClaim()
    {
        // Arrange
        var identity = BuildIdentity();
        var claim = new Claim("new_type", "new_value");

        // Act
        identity.AddOrReplace(claim);

        // Assert
        identity.FindFirst("new_type").ShouldNotBeNull();
        identity.FindFirst("new_type")!.Value.ShouldBe("new_value");
    }

    /// <summary>
    /// 测试目的：AddOrReplace 在声明已存在时，应替换原有值，并只保留一条声明。
    /// </summary>
    [Fact]
    public void AddOrReplace_WhenClaimAlreadyExists_ShouldReplaceWithNewValue()
    {
        // Arrange
        var identity = BuildIdentity(new Claim("replace_type", "old_value"));
        var replacement = new Claim("replace_type", "replaced_value");

        // Act
        var returned = identity.AddOrReplace(replacement);

        // Assert
        identity.FindAll("replace_type").Count().ShouldBe(1);
        identity.FindFirst("replace_type")!.Value.ShouldBe("replaced_value");
        returned.ShouldBeSameAs(identity);
    }

    /// <summary>
    /// 测试目的：AddOrReplace 在存在多条同类型声明时，应全部移除后添加新声明，只保留一条。
    /// </summary>
    [Fact]
    public void AddOrReplace_WhenMultipleClaimsOfSameType_ShouldReplaceAll()
    {
        // Arrange
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("multi_type", "val1"));
        identity.AddClaim(new Claim("multi_type", "val2"));
        var replacement = new Claim("multi_type", "single_value");

        // Act
        identity.AddOrReplace(replacement);

        // Assert
        identity.FindAll("multi_type").Count().ShouldBe(1);
        identity.FindFirst("multi_type")!.Value.ShouldBe("single_value");
    }

    // ═══════════════════════════════════════════════════════════
    // AddIdentityIfNotContains — ClaimsPrincipal
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddIdentityIfNotContains 在主体中没有同认证类型的标识时，应添加该标识。
    /// </summary>
    [Fact]
    public void AddIdentityIfNotContains_WhenAuthTypeNotExists_ShouldAddIdentity()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "ExistingAuth"));
        var newIdentity = new ClaimsIdentity(new[] { new Claim("x", "1") }, "NewAuth");

        // Act
        var returned = principal.AddIdentityIfNotContains(newIdentity);

        // Assert
        principal.Identities.Count().ShouldBe(2);
        principal.Identities.Any(i => i.AuthenticationType == "NewAuth").ShouldBeTrue();
        returned.ShouldBeSameAs(principal);
    }

    /// <summary>
    /// 测试目的：AddIdentityIfNotContains 在主体中已有相同认证类型的标识时，不应重复添加。
    /// </summary>
    [Fact]
    public void AddIdentityIfNotContains_WhenAuthTypeAlreadyExists_ShouldNotDuplicate()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "SameAuth"));
        var duplicate = new ClaimsIdentity(new[] { new Claim("y", "2") }, "SameAuth");

        // Act
        principal.AddIdentityIfNotContains(duplicate);

        // Assert
        principal.Identities.Count().ShouldBe(1);
    }

    // ═══════════════════════════════════════════════════════════
    // RemoveAll — ClaimsIdentity
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：RemoveAll 在存在多条同类型声明时，应全部移除，声明数变为 0。
    /// </summary>
    [Fact]
    public void RemoveAll_WhenMultipleClaimsOfType_ShouldRemoveAll()
    {
        // Arrange
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("remove_type", "a"));
        identity.AddClaim(new Claim("remove_type", "b"));
        identity.AddClaim(new Claim("keep_type", "keep"));

        // Act
        var returned = identity.RemoveAll("remove_type");

        // Assert
        identity.FindAll("remove_type").ShouldBeEmpty();
        identity.FindFirst("keep_type").ShouldNotBeNull(); // 不影响其他类型
        returned.ShouldBeSameAs(identity);
    }

    /// <summary>
    /// 测试目的：RemoveAll 在声明不存在时，应无副作用，不抛异常。
    /// </summary>
    [Fact]
    public void RemoveAll_WhenClaimTypeNotExist_ShouldNotThrow()
    {
        // Arrange
        var identity = BuildIdentity(new Claim("other_type", "value"));

        // Act & Assert（不应抛出任何异常）
        Should.NotThrow(() => identity.RemoveAll("nonexistent_type"));
        identity.FindFirst("other_type").ShouldNotBeNull(); // 原有声明不受影响
    }
}
