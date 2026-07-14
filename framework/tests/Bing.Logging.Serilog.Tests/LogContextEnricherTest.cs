using Bing.Logging.Serilog.Enrichers;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LogContextEnricher"/> 单元测试
/// </summary>
public class LogContextEnricherTest
{
    /// <summary>
    /// 测试目的：日志快照应映射为结构化属性，且显式属性不能被自动上下文覆盖。
    /// </summary>
    [Fact]
    public void Enrich_WhenExplicitPropertyExists_ShouldKeepExplicitValueAndAddSnapshotProperties()
    {
        // Arrange
        var snapshot = new LogContextSnapshot(
            "trace-001",
            new LogIdentityContext("user-001", "tenant-context", "session-001"),
            new LogClientContext("app", "Production", browser: "browser"),
            new BusinessLogContext(tags: new[] { "tag-a" }, data: new Dictionary<string, object> { ["Extra"] = "value" }));
        var accessor = new Mock<ILogContextAccessor>();
        accessor.SetupGet(x => x.Current).Returns(snapshot);
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .Enrich.With(new LogContextEnricher(accessor.Object))
            .WriteTo.Sink(sink)
            .CreateLogger();

        // Act
        logger.ForContext("TenantId", "tenant-explicit").Information("message");

        // Assert
        var logEvent = sink.Events.ShouldHaveSingleItem();
        GetScalar(logEvent, "TraceId").ShouldBe("trace-001");
        GetScalar(logEvent, "UserId").ShouldBe("user-001");
        GetScalar(logEvent, "TenantId").ShouldBe("tenant-explicit");
        GetScalar(logEvent, "Browser").ShouldBe("browser");
        GetScalar(logEvent, "Extra").ShouldBe("value");
        logEvent.Properties.ContainsKey("Tags").ShouldBeTrue();
    }

    private static object GetScalar(LogEvent logEvent, string name) =>
        ((ScalarValue)logEvent.Properties[name]).Value;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}