using System.Security.Claims;
using Bing.Security.Claims;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Claims;

/// <summary>
/// <see cref="BingClaimTypes"/> 单元测试
/// </summary>
public class BingClaimTypesTest
{
    // ═══════════════════════════════════════════════════════════
    // 默认值验证
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：UserName 默认值应为 ClaimTypes.Name，符合 MS 标准声明类型。
    /// </summary>
    [Fact]
    public void UserName_Default_ShouldBeClaimTypesName()
    {
        BingClaimTypes.UserName.ShouldBe(ClaimTypes.Name);
    }

    /// <summary>
    /// 测试目的：UserId 默认值应为 ClaimTypes.NameIdentifier。
    /// </summary>
    [Fact]
    public void UserId_Default_ShouldBeNameIdentifier()
    {
        BingClaimTypes.UserId.ShouldBe(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// 测试目的：Email 默认值应为 ClaimTypes.Email。
    /// </summary>
    [Fact]
    public void Email_Default_ShouldBeClaimTypesEmail()
    {
        BingClaimTypes.Email.ShouldBe(ClaimTypes.Email);
    }

    /// <summary>
    /// 测试目的：Role 默认值应为 ClaimTypes.Role。
    /// </summary>
    [Fact]
    public void Role_Default_ShouldBeClaimTypesRole()
    {
        BingClaimTypes.Role.ShouldBe(ClaimTypes.Role);
    }

    /// <summary>
    /// 测试目的：PhoneNumber 默认值应为 "phone_number"。
    /// </summary>
    [Fact]
    public void PhoneNumber_Default_ShouldBePhoneNumber()
    {
        BingClaimTypes.PhoneNumber.ShouldBe("phone_number");
    }

    /// <summary>
    /// 测试目的：TenantId 默认值应为 "tenant_id"。
    /// </summary>
    [Fact]
    public void TenantId_Default_ShouldBeTenantId()
    {
        BingClaimTypes.TenantId.ShouldBe("tenant_id");
    }

    /// <summary>
    /// 测试目的：ClientId 默认值应为 "client_id"。
    /// </summary>
    [Fact]
    public void ClientId_Default_ShouldBeClientId()
    {
        BingClaimTypes.ClientId.ShouldBe("client_id");
    }

    /// <summary>
    /// 测试目的：SessionId 默认值应为 "session_id"。
    /// </summary>
    [Fact]
    public void SessionId_Default_ShouldBeSessionId()
    {
        BingClaimTypes.SessionId.ShouldBe("session_id");
    }

    // ═══════════════════════════════════════════════════════════
    // 可覆盖性（静态属性可运行时替换）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：静态属性可被运行时覆盖，验证替换后能正确读回新值（恢复原值）。
    /// </summary>
    [Fact]
    public void UserName_WhenOverridden_ShouldReturnNewValue()
    {
        // Arrange
        var original = BingClaimTypes.UserName;
        try
        {
            // Act
            BingClaimTypes.UserName = "custom_username";

            // Assert
            BingClaimTypes.UserName.ShouldBe("custom_username");
        }
        finally
        {
            // Restore
            BingClaimTypes.UserName = original;
        }
    }
}
