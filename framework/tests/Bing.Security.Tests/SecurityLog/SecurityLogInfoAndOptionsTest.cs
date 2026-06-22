using Bing.SecurityLog;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.SecurityLog;

/// <summary>
/// <see cref="SecurityLogInfo"/> 及 <see cref="BingSecurityLogOptions"/> 单元测试
/// </summary>
public class SecurityLogInfoAndOptionsTest
{
    // ═══════════════════════════════════════════════════════════
    // SecurityLogInfo
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 ExtraProperties 不为 null，防止使用时空引用异常。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_Default_ExtraPropertiesShouldNotBeNull()
    {
        // Arrange & Act
        var info = new SecurityLogInfo();

        // Assert
        info.ExtraProperties.ShouldNotBeNull();
        info.ExtraProperties.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：ApplicationName 属性可读写，用于标识产生日志的服务。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_ApplicationName_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.ApplicationName = "MyApp";

        // Assert
        info.ApplicationName.ShouldBe("MyApp");
    }

    /// <summary>
    /// 测试目的：Identity 属性可读写，记录当前安全操作的身份标识。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_Identity_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.Identity = "Login";

        // Assert
        info.Identity.ShouldBe("Login");
    }

    /// <summary>
    /// 测试目的：Action 属性可读写，记录触发安全日志的具体操作。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_Action_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.Action = "UserLogin";

        // Assert
        info.Action.ShouldBe("UserLogin");
    }

    /// <summary>
    /// 测试目的：UserId 与 UserName 属性可读写，用于记录操作者信息。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_UserFields_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.UserId = "uid-001";
        info.UserName = "zhangsan";

        // Assert
        info.UserId.ShouldBe("uid-001");
        info.UserName.ShouldBe("zhangsan");
    }

    /// <summary>
    /// 测试目的：TenantId 与 TenantName 属性可读写，用于多租户场景下标识租户信息。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_TenantFields_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.TenantId = "tenant-001";
        info.TenantName = "示例租户";

        // Assert
        info.TenantId.ShouldBe("tenant-001");
        info.TenantName.ShouldBe("示例租户");
    }

    /// <summary>
    /// 测试目的：ClientId、ClientIpAddress 及 BrowserInfo 属性可读写，用于客户端追踪。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_ClientFields_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.ClientId = "web-client";
        info.ClientIpAddress = "192.168.1.100";
        info.BrowserInfo = "Chrome/120";

        // Assert
        info.ClientId.ShouldBe("web-client");
        info.ClientIpAddress.ShouldBe("192.168.1.100");
        info.BrowserInfo.ShouldBe("Chrome/120");
    }

    /// <summary>
    /// 测试目的：CorrelationId 属性可读写，用于日志链路追踪。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_CorrelationId_ShouldBeReadWritable()
    {
        // Arrange
        var info = new SecurityLogInfo();
        var traceId = Guid.NewGuid().ToString();

        // Act
        info.CorrelationId = traceId;

        // Assert
        info.CorrelationId.ShouldBe(traceId);
    }

    /// <summary>
    /// 测试目的：ExtraProperties 字典支持添加自定义键值对，用于扩展字段存储。
    /// </summary>
    [Fact]
    public void SecurityLogInfo_ExtraProperties_CanAddEntries()
    {
        // Arrange
        var info = new SecurityLogInfo();

        // Act
        info.ExtraProperties["device"] = "mobile";
        info.ExtraProperties["os"] = "iOS";

        // Assert
        info.ExtraProperties["device"].ShouldBe("mobile");
        info.ExtraProperties["os"].ShouldBe("iOS");
        info.ExtraProperties.Count.ShouldBe(2);
    }

    // ═══════════════════════════════════════════════════════════
    // BingSecurityLogOptions
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：默认构造后 IsEnabled 应为 true，安全日志默认开启。
    /// </summary>
    [Fact]
    public void BingSecurityLogOptions_Default_IsEnabledShouldBeTrue()
    {
        // Arrange & Act
        var options = new BingSecurityLogOptions();

        // Assert
        options.IsEnabled.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：IsEnabled 可设置为 false，支持在特定环境关闭安全日志。
    /// </summary>
    [Fact]
    public void BingSecurityLogOptions_IsEnabled_CanBeDisabled()
    {
        // Arrange
        var options = new BingSecurityLogOptions();

        // Act
        options.IsEnabled = false;

        // Assert
        options.IsEnabled.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：ApplicationName 属性可读写，用于标识当前应用在安全日志中的名称。
    /// </summary>
    [Fact]
    public void BingSecurityLogOptions_ApplicationName_ShouldBeReadWritable()
    {
        // Arrange
        var options = new BingSecurityLogOptions();

        // Act
        options.ApplicationName = "OrderService";

        // Assert
        options.ApplicationName.ShouldBe("OrderService");
    }

    /// <summary>
    /// 测试目的：默认构造后 ApplicationName 为 null，允许调用方按需配置。
    /// </summary>
    [Fact]
    public void BingSecurityLogOptions_Default_ApplicationNameShouldBeNull()
    {
        // Arrange & Act
        var options = new BingSecurityLogOptions();

        // Assert
        options.ApplicationName.ShouldBeNull();
    }
}
