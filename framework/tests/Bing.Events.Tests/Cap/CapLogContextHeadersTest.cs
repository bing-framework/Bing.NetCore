using Bing.Events.Cap;
using Bing.Logging;
using Shouldly;
using Xunit;

namespace Bing.Events.Tests.Cap;

/// <summary>
/// <see cref="CapLogContextHeaders"/> 单元测试
/// </summary>
public class CapLogContextHeadersTest
{
    /// <summary>
    /// 测试目的：完整日志上下文写入 CAP Header 后应能无损恢复关键字段。
    /// </summary>
    [Fact]
    public void WriteAndRead_WithCompleteSnapshot_ShouldRoundTrip()
    {
        // Arrange
        var snapshot = new LogContextSnapshot(
            "trace-001",
            new LogIdentityContext("user-001", "tenant-001", "session-001"),
            new LogClientContext("app", "Production", "127.0.0.1", "host", "browser", "https://localhost", true),
            new BusinessLogContext("business-001", new[] { "tag-a" }, new Dictionary<string, object> { ["OrderId"] = "1" }));
        var headers = new Dictionary<string, string>();

        // Act
        CapLogContextHeaders.Write(headers, snapshot);
        var restored = CapLogContextHeaders.Read(headers, "fallback");

        // Assert
        restored.TraceId.ShouldBe("trace-001");
        restored.Identity.UserId.ShouldBe("user-001");
        restored.Identity.TenantId.ShouldBe("tenant-001");
        restored.Client.Browser.ShouldBe("browser");
        restored.Business.BusinessTraceId.ShouldBe("business-001");
        restored.Business.Tags.ShouldContain("tag-a");
        restored.Business.Data.ContainsKey("OrderId").ShouldBeTrue();
    }
}