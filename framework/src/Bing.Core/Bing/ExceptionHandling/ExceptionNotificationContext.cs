using Microsoft.Extensions.Logging;

namespace Bing.ExceptionHandling;

/// <summary>
/// 保存异常通知期间共享的异常、日志级别和处理状态。
/// </summary>
public class ExceptionNotificationContext
{
    /// <summary>
    /// 获取要通知的异常对象。
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// 获取异常对应的日志级别。
    /// </summary>
    public LogLevel LogLevel { get; }

    /// <summary>
    /// 获取或设置异常是否已由当前通知链处理。
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// 使用异常和可选通知元数据初始化 <see cref="ExceptionNotificationContext"/> 的实例。
    /// </summary>
    /// <param name="exception">要通知的异常，不能为 <c>null</c>。</param>
    /// <param name="logLevel">可选的日志级别；为空时根据异常类型推导。</param>
    /// <param name="handled">初始处理状态，默认值为 <c>true</c>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 <c>null</c> 时抛出。</exception>
    public ExceptionNotificationContext(Exception exception, LogLevel? logLevel = null, bool handled = true)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        LogLevel = logLevel ?? exception.GetLogLevel();
        Handled = handled;
    }
}
