using Bing.Logging.Core;

namespace Bing.Logging;

/// <summary>
/// 日志操作
/// </summary>
/// <typeparam name="TCategoryName">日志类别</typeparam>
public interface ILog<out TCategoryName> : ILog
{
}

/// <summary>
/// 日志操作
/// </summary>
public interface ILog
{
    /// <summary>
    /// 设置日志事件标识
    /// </summary>
    /// <param name="eventId">日志事件标识</param>
    /// <returns>当前日志操作实例。</returns>
    ILog EventId(EventId eventId);

    /// <summary>
    /// 设置异常
    /// </summary>
    /// <param name="exception">异常</param>
    /// <returns>当前日志操作实例。</returns>
    ILog Exception(Exception exception);

    /// <summary>
    /// 设置自定义属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="propertyValue">属性值</param>
    /// <returns>当前日志操作实例。</returns>
    ILog Property(string propertyName, string propertyValue);

    /// <summary>
    /// 设置日志事件描述符
    /// </summary>
    /// <param name="action">操作</param>
    /// <returns>当前日志操作实例。</returns>
    ILog Set(Action<LogEventDescriptor> action);

    /// <summary>
    /// 设置日志状态对象
    /// </summary>
    /// <param name="state">状态对象</param>
    /// <returns>当前日志操作实例。</returns>
    ILog State(object state);

    /// <summary>
    /// 设置日志消息
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="args">日志消息参数</param>
    /// <returns>当前日志操作实例。</returns>
    ILog Message(string message, params object[] args);

    /// <summary>
    /// 是否启用
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    /// <returns>true=启用；false=禁用</returns>
    bool IsEnabled(LogLevel logLevel);

    /// <summary>
    /// 开启日志范围
    /// </summary>
    /// <typeparam name="TState">日志状态类型</typeparam>
    /// <param name="state">日志状态</param>
    /// <returns>用于结束当前日志范围的释放句柄。</returns>
    IDisposable BeginScope<TState>(TState state);

    /// <summary>
    /// 写跟踪日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogTrace();

    /// <summary>
    /// 写调试日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogDebug();

    /// <summary>
    /// 写信息日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogInformation();

    /// <summary>
    /// 写警告日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogWarning();

    /// <summary>
    /// 写错误日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogError();

    /// <summary>
    /// 写致命日志
    /// </summary>
    /// <returns>当前日志操作实例。</returns>
    ILog LogCritical();
}
