using Bing.Logging.Core.Callers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests;

/// <summary>
/// <see cref="LoggerWrapper"/> 单元测试
/// </summary>
public class LoggerWrapperTest
{
    // ═══════════════════════════════════════════════════════════
    // 构造器校验
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：传入 null ILogger 时，构造器应抛出 ArgumentNullException，
    /// 防止在后续调用时触发 NullReferenceException。
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new LoggerWrapper(null));
    }

    // ═══════════════════════════════════════════════════════════
    // IsEnabled 委托
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：IsEnabled 应委托给底层 ILogger，返回 ILogger 的判断结果，
    /// 确保级别过滤行为与底层提供程序一致。
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace, true)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Critical, true)]
    public void IsEnabled_ShouldDelegateToUnderlyingLogger(LogLevel level, bool expected)
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.IsEnabled(level)).Returns(expected);
        var wrapper = new LoggerWrapper(mockLogger.Object);

        // Act
        var result = wrapper.IsEnabled(level);

        // Assert
        result.ShouldBe(expected);
        mockLogger.Verify(l => l.IsEnabled(level), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // BeginScope 委托
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：BeginScope 应委托给底层 ILogger 并返回其 IDisposable，
    /// 确保结构化日志作用域行为不被截断。
    /// </summary>
    [Fact]
    public void BeginScope_ShouldDelegateToUnderlyingLogger()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockScope = new Mock<IDisposable>();
        mockLogger
            .Setup(l => l.BeginScope(It.IsAny<string>()))
            .Returns(mockScope.Object);
        var wrapper = new LoggerWrapper(mockLogger.Object);

        // Act
        var scope = wrapper.BeginScope("test-scope");

        // Assert
        scope.ShouldBeSameAs(mockScope.Object);
        mockLogger.Verify(l => l.BeginScope(It.IsAny<string>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // Log 委托（Log<TState>）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Log&lt;TState&gt; 应委托给底层 ILogger.Log 一次，
    /// 确保日志写入操作不丢失。
    /// </summary>
    [Fact]
    public void Log_ShouldDelegateToUnderlyingLogger()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var wrapper = new LoggerWrapper(mockLogger.Object);
        var eventId = new EventId(1, "test");

        // Act
        wrapper.Log(LogLevel.Information, eventId, "state", null, (s, e) => s);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                eventId,
                It.IsAny<string>(),
                null,
                It.IsAny<Func<string, Exception, string>>()),
            Times.Once);
    }

    // ═══════════════════════════════════════════════════════════
    // 各级别便捷方法委托
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：LogTrace / LogDebug / LogInformation / LogWarning / LogError / LogCritical
    /// 均应委托给底层 ILogger 写入对应级别的日志，
    /// 确保便捷方法不会静默丢弃日志。
    /// </summary>
    [Theory]
    [InlineData(LogLevel.Trace)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Warning)]
    [InlineData(LogLevel.Error)]
    [InlineData(LogLevel.Critical)]
    public void LogAtLevel_ShouldDelegateToUnderlyingLoggerWithCorrectLevel(LogLevel level)
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var wrapper = new LoggerWrapper(mockLogger.Object);
        var eventId = new EventId(0);

        // Act
        switch (level)
        {
            case LogLevel.Trace:       wrapper.LogTrace(eventId, null, "trace"); break;
            case LogLevel.Debug:       wrapper.LogDebug(eventId, null, "debug"); break;
            case LogLevel.Information: wrapper.LogInformation(eventId, null, "info"); break;
            case LogLevel.Warning:     wrapper.LogWarning(eventId, null, "warn"); break;
            case LogLevel.Error:       wrapper.LogError(eventId, null, "error"); break;
            case LogLevel.Critical:    wrapper.LogCritical(eventId, null, "critical"); break;
        }

        // Assert — 只要底层 Log 被调用了对应级别即可
        mockLogger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once,
            $"LogLevel.{level} 应委托到底层 ILogger.Log");
    }
}
