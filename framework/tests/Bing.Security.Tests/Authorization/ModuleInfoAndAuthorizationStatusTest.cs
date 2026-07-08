using Bing.Authorization.Modules;
using Bing.Security;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Authorization;

/// <summary>
/// <see cref="ModuleInfo"/> 及 <see cref="AuthorizationStatus"/> 单元测试
/// </summary>
public class ModuleInfoAndAuthorizationStatusTest
{
    // ═══════════════════════════════════════════════════════════
    // ModuleInfo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后所有属性均为 null / 0，确保无意外默认值。
    /// </summary>
    [Fact]
    public void ModuleInfo_Default_AllPropertiesShouldBeNullOrDefault()
    {
        // Arrange & Act
        var info = new ModuleInfo();

        // Assert
        info.Code.ShouldBeNull();
        info.Name.ShouldBeNull();
        info.SortId.ShouldBe(0);
        info.Position.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：属性赋值后应能正确读取，确保属性是可读写的。
    /// </summary>
    [Fact]
    public void ModuleInfo_SetProperties_ShouldReturnAssignedValues()
    {
        // Arrange & Act
        var info = new ModuleInfo
        {
            Code = "user.manage",
            Name = "用户管理",
            SortId = 10,
            Position = "system.admin"
        };

        // Assert
        info.Code.ShouldBe("user.manage");
        info.Name.ShouldBe("用户管理");
        info.SortId.ShouldBe(10);
        info.Position.ShouldBe("system.admin");
    }

    /// <summary>
    /// 测试目的：SortId 可以设置为负数（如排在最前），确保 int 类型无下界限制。
    /// </summary>
    [Fact]
    public void ModuleInfo_NegativeSortId_ShouldBeAccepted()
    {
        // Arrange & Act
        var info = new ModuleInfo { SortId = -1 };

        // Assert
        info.SortId.ShouldBe(-1);
    }

    // ═══════════════════════════════════════════════════════════
    // AuthorizationStatus
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：验证枚举各成员的数值符合规范（HTTP 状态码对应值）。
    /// </summary>
    [Fact]
    public void AuthorizationStatus_Values_ShouldMatchExpected()
    {
        // Assert
        ((int)AuthorizationStatus.Ok).ShouldBe(200);
        ((int)AuthorizationStatus.Unauthorized).ShouldBe(401);
        ((int)AuthorizationStatus.LoginTimeout).ShouldBe(402);
        ((int)AuthorizationStatus.Forbidden).ShouldBe(403);
        ((int)AuthorizationStatus.NoFound).ShouldBe(404);
        ((int)AuthorizationStatus.Locked).ShouldBe(423);
        ((int)AuthorizationStatus.OtherDeviceLogin).ShouldBe(424);
        ((int)AuthorizationStatus.Error).ShouldBe(500);
    }

    /// <summary>
    /// 测试目的：枚举成员个数为 8，防止因新增/删除而导致使用方行为异常（用于变更感知）。
    /// </summary>
    [Fact]
    public void AuthorizationStatus_EnumCount_ShouldBe8()
    {
        // Assert
        Enum.GetValues(typeof(AuthorizationStatus)).Length.ShouldBe(8);
    }

    /// <summary>
    /// 测试目的：AuthorizationStatus.Ok 的值可成功转换为 int，确保用于 HTTP 响应码时的兼容性。
    /// </summary>
    [Fact]
    public void AuthorizationStatus_OkValue_ShouldEqualHttpStatus200()
    {
        // Arrange
        const int httpOk = 200;

        // Act
        var status = AuthorizationStatus.Ok;

        // Assert
        ((int)status).ShouldBe(httpOk);
    }
}
