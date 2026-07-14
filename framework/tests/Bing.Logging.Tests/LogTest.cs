using Moq;
using Microsoft.Extensions.Logging;

namespace Bing.Logging.Tests;

/// <summary>
/// 日志操作测试
/// </summary>
public partial class LogTest
{
    /// <summary>
    /// 模拟日志
    /// </summary>
    private readonly Mock<ILoggerWrapper> _mockLogger;

    /// <summary>
    /// 日志操作
    /// </summary>
    private readonly ILog _log;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public LogTest()
    {
        _mockLogger = new Mock<ILoggerWrapper>();
        _log = new Log(_mockLogger.Object);
    }

    /// <summary>
    /// 测试目的：并行异步流复用同一日志实例时，消息状态应隔离且不能互相清理。
    /// </summary>
    [Fact]
    public async Task LogInformation_WhenSharedAcrossParallelFlows_ShouldKeepEventStateIsolated()
    {
        // Arrange
        var barrier = new Barrier(2);

        // Act
        var first = Task.Run(() => Write("first"));
        var second = Task.Run(() => Write("second"));
        await Task.WhenAll(first, second);

        // Assert
        _mockLogger.Verify(x => x.Log(LogLevel.Information, 0, null, "first", It.Is<object[]>(args => args.Length == 0)), Times.Once);
        _mockLogger.Verify(x => x.Log(LogLevel.Information, 0, null, "second", It.Is<object[]>(args => args.Length == 0)), Times.Once);

        void Write(string message)
        {
            _log.Message(message);
            barrier.SignalAndWait();
            _log.LogInformation();
        }
    }
}