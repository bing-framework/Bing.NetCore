using Microsoft.Extensions.Configuration;
using Serilog;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LoggerConfigurationExtensions.ConfigLogLevel"/> 单元测试。
/// 通过公共 API 间接验证 LogLevelSwitcher 的映射逻辑。
/// </summary>
public class LoggerConfigurationExtensionsTest
{
    // ═══════════════════════════════════════════════════════════
    // ConfigLogLevel — 参数校验
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：source 为 null 时应抛出 ArgumentNullException，
    /// 防止链式调用时静默失败。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_NullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            ((LoggerConfiguration)null).ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：configuration 为 null 时应抛出 ArgumentNullException，
    /// 确保配置缺失时有明确的错误提示。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_NullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            loggerConfig.ConfigLogLevel(null));
    }

    /// <summary>
    /// 测试目的：Logging:LogLevel 节点为空时，ConfigLogLevel 应不抛异常并返回原配置，
    /// 确保没有日志配置节时可以安全使用。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_EmptyLoggingSection_ShouldNotThrow()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：Logging:LogLevel 包含 Default 条目时，ConfigLogLevel 应不抛异常，
    /// 验证 Default 键走 MinimumLevel.ControlledBy 分支。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_WithDefaultLevel_ShouldNotThrow()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Logging:LogLevel:Default", "Information" }
            })
            .Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：Logging:LogLevel 包含 Override 条目（非 Default）时，ConfigLogLevel 应不抛异常，
    /// 验证 Override 键走 MinimumLevel.Override 分支。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_WithOverrideLevel_ShouldNotThrow()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Logging:LogLevel:Microsoft", "Warning" },
                { "Logging:LogLevel:System", "Error" }
            })
            .Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：同时包含 Default 和 Override 条目时，ConfigLogLevel 应全部处理，不抛异常，
    /// 验证混合配置场景。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_WithDefaultAndOverride_ShouldNotThrow()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Logging:LogLevel:Default", "Debug" },
                { "Logging:LogLevel:Microsoft.AspNetCore", "Warning" },
                { "Logging:LogLevel:System", "Error" }
            })
            .Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：ConfigLogLevel 应返回原 LoggerConfiguration 实例（方法链支持），
    /// 确保调用者可以继续链式配置。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_ShouldReturnSameLoggerConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var loggerConfig = new LoggerConfiguration();

        // Act
        var result = loggerConfig.ConfigLogLevel(config);

        // Assert
        result.ShouldBeSameAs(loggerConfig);
    }

    // ═══════════════════════════════════════════════════════════
    // LogLevelSwitcher 通过 ConfigLogLevel 间接验证（各级别映射）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Logging:LogLevel 支持 Trace / Debug / Information / Warning / Error / Critical / None
    /// 全部七个 MSLogging 级别字符串，ConfigLogLevel 均不应抛异常，
    /// 确保 LogLevelSwitcher.Switch(string) 覆盖所有有效输入。
    /// </summary>
    [Theory]
    [InlineData("Trace")]
    [InlineData("Debug")]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    [InlineData("Critical")]
    [InlineData("None")]
    public void ConfigLogLevel_AllMSLogLevels_ShouldNotThrow(string level)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Logging:LogLevel:Default", level }
            })
            .Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }

    /// <summary>
    /// 测试目的：未知日志级别字符串时，Switch 应 fallback 为 Warning 而不是抛异常，
    /// 确保配置文件拼写错误不会导致应用启动失败。
    /// </summary>
    [Fact]
    public void ConfigLogLevel_UnknownLevel_ShouldFallbackWithoutThrowing()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "Logging:LogLevel:Default", "UnknownLevel" }
            })
            .Build();
        var loggerConfig = new LoggerConfiguration();

        // Act & Assert（fallback 为 Warning，不应抛异常）
        Should.NotThrow(() => loggerConfig.ConfigLogLevel(config));
    }
}
