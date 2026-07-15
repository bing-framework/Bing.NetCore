using Bing.Logging.Serilog;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LoggerEnrichmentConfigurationExtensions"/> 单元测试
/// </summary>
public class LoggerEnrichmentConfigurationExtensionsTest
{
    // ─────────────────────────────────────────────────────────
    // 辅助：获取有效的 LoggerEnrichmentConfiguration
    // ─────────────────────────────────────────────────────────
    private static LoggerEnrichmentConfiguration GetEnrichConfig()
        => new LoggerConfiguration().Enrich;

    // ═══════════════════════════════════════════════════════════
    // WithLogContext
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithLogContext(null) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithLogContext_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithLogContext(null!));
    }

    /// <summary>
    /// 测试目的：WithLogContext 对有效 source 应返回非 null 的 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithLogContext_WithValidSource_ShouldReturnLoggerConfiguration()
    {
        var result = GetEnrichConfig().WithLogContext();
        result.ShouldNotBeNull();
        result.ShouldBeOfType<LoggerConfiguration>();
    }

    // ═══════════════════════════════════════════════════════════
    // WithLogLevel
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithLogLevel(null) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithLogLevel_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithLogLevel(null!));
    }

    /// <summary>
    /// 测试目的：WithLogLevel 对有效 source 应返回非 null 的 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithLogLevel_WithValidSource_ShouldReturnLoggerConfiguration()
    {
        var result = GetEnrichConfig().WithLogLevel();
        result.ShouldNotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // WithProperty
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithProperty(null, kv) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithProperty_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        var kv = new KeyValuePair<string, object>("key", "value");
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithProperty(null!, kv));
    }

    /// <summary>
    /// 测试目的：WithProperty(source, default) 应抛出 ArgumentNullException（key=null 的默认键值对）。
    /// </summary>
    [Fact]
    public void WithProperty_WhenKeyValueIsDefault_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            GetEnrichConfig().WithProperty(default));
    }

    /// <summary>
    /// 测试目的：WithProperty 对有效键值对应返回非 null 的 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithProperty_WithValidKeyValue_ShouldReturnLoggerConfiguration()
    {
        var kv = new KeyValuePair<string, object>("env", "production");
        var result = GetEnrichConfig().WithProperty(kv);
        result.ShouldNotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // WithFunction(key, Func<string>)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithFunction(null, key, func) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithFunction_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithFunction(null!, "key", () => "val"));
    }

    /// <summary>
    /// 测试目的：WithFunction 对 key 为 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithFunction_WhenKeyIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            GetEnrichConfig().WithFunction(null!, () => "val"));
    }

    /// <summary>
    /// 测试目的：WithFunction 对 key 为空字符串应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithFunction_WhenKeyIsEmpty_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            GetEnrichConfig().WithFunction("", () => "val"));
    }

    /// <summary>
    /// 测试目的：WithFunction 对 func 为 null 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithFunction_WhenFuncIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            GetEnrichConfig().WithFunction("key", (Func<string>)null!));
    }

    /// <summary>
    /// 测试目的：WithFunction 对有效参数应返回非 null 的 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithFunction_WithValidArgs_ShouldReturnLoggerConfiguration()
    {
        var result = GetEnrichConfig().WithFunction("requestId", () => Guid.NewGuid().ToString());
        result.ShouldNotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // WithFunction(key, Func<LogEvent, string>)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithFunction(source, key, Func&lt;LogEvent,string&gt;) 对 null source 应抛出。
    /// </summary>
    [Fact]
    public void WithFunctionLogEvent_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithFunction(null!, "key", (LogEvent _) => "val"));
    }

    /// <summary>
    /// 测试目的：WithFunction(source, key, Func&lt;LogEvent,string&gt;) 对有效参数应返回 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithFunctionLogEvent_WithValidArgs_ShouldReturnLoggerConfiguration()
    {
        var result = GetEnrichConfig().WithFunction("level", (LogEvent e) => e.Level.ToString());
        result.ShouldNotBeNull();
    }

    // ═══════════════════════════════════════════════════════════
    // WithEnvironment
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：WithEnvironment(null, varName) 应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithEnvironment_WhenSourceIsNull_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            LoggerEnrichmentConfigurationExtensions.WithEnvironment(null!, "ASPNETCORE_ENVIRONMENT"));
    }

    /// <summary>
    /// 测试目的：WithEnvironment 对空变量名应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void WithEnvironment_WhenVariableNameIsEmpty_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            GetEnrichConfig().WithEnvironment(""));
    }

    /// <summary>
    /// 测试目的：WithEnvironment 对有效变量名应返回非 null 的 LoggerConfiguration。
    /// </summary>
    [Fact]
    public void WithEnvironment_WithValidName_ShouldReturnLoggerConfiguration()
    {
        var result = GetEnrichConfig().WithEnvironment("ASPNETCORE_ENVIRONMENT");
        result.ShouldNotBeNull();
    }
}
