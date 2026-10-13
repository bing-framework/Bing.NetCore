using Moq;

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
    /// 初始化一个 <see cref="LogTest"/> 类型的实例。
    /// </summary>
    public LogTest()
    {
        _mockLogger = new Mock<ILoggerWrapper>();
        _log = new Log(_mockLogger.Object);
    }
}