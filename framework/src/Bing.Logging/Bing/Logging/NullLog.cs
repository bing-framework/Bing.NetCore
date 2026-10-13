using Bing.Logging.Core;

namespace Bing.Logging;

/// <summary>
/// 空日志操作
/// </summary>
/// <typeparam name="TCategoryName">日志类别</typeparam>
public class NullLog<TCategoryName> : ILog<TCategoryName>
{
    /// <summary>
    /// 空日志操作实例
    /// </summary>
    public static readonly ILog<TCategoryName> Instance = new NullLog<TCategoryName>();

    /// <inheritdoc />
    public ILog EventId(EventId eventId) => this;

    /// <inheritdoc />
    public ILog Exception(Exception exception) => this;

    /// <inheritdoc />
    public ILog Property(string propertyName, string propertyValue) => this;

    /// <summary>
    /// 设置扩展属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="propertyValue">属性值</param>
    /// <returns>当前空日志操作实例。</returns>
    public ILog ExtraProperty(string propertyName, object propertyValue) => this;

    /// <inheritdoc />
    public ILog Set(Action<LogEventDescriptor> action) => this;

    /// <summary>
    /// 设置标签
    /// </summary>
    /// <param name="tags">标签</param>
    /// <returns>当前空日志操作实例。</returns>
    public ILog Tags(params string[] tags) => this;

    /// <inheritdoc />
    public ILog State(object state) => this;

    /// <inheritdoc />
    public ILog Message(string message, params object[] args) => this;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => false;

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) => new DisposeAction(() => { });

    /// <inheritdoc />
    public ILog LogTrace() => this;

    /// <inheritdoc />
    public ILog LogDebug() => this;

    /// <inheritdoc />
    public ILog LogInformation() => this;

    /// <inheritdoc />
    public ILog LogWarning() => this;

    /// <inheritdoc />
    public ILog LogError() => this;

    /// <inheritdoc />
    public ILog LogCritical() => this;
}

/// <summary>
/// 空日志操作
/// </summary>
public class NullLog : ILog
{
    /// <summary>
    /// 空日志操作实例
    /// </summary>
    public static readonly ILog Instance = new NullLog();

    /// <inheritdoc />
    public ILog EventId(EventId eventId) => this;

    /// <inheritdoc />
    public ILog Exception(Exception exception) => this;

    /// <inheritdoc />
    public ILog Property(string propertyName, string propertyValue) => this;

    /// <summary>
    /// 设置扩展属性
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="propertyValue">属性值</param>
    /// <returns>当前空日志操作实例。</returns>
    public ILog ExtraProperty(string propertyName, object propertyValue) => this;

    /// <inheritdoc />
    public ILog Set(Action<LogEventDescriptor> action) => this;

    /// <summary>
    /// 设置标签
    /// </summary>
    /// <param name="tags">标签</param>
    /// <returns>当前空日志操作实例。</returns>
    public ILog Tags(params string[] tags) => this;

    /// <inheritdoc />
    public ILog State(object state) => this;

    /// <inheritdoc />
    public ILog Message(string message, params object[] args) => this;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => false;

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) => new DisposeAction(() => { });

    /// <inheritdoc />
    public ILog LogTrace() => this;

    /// <inheritdoc />
    public ILog LogDebug() => this;

    /// <inheritdoc />
    public ILog LogInformation() => this;

    /// <inheritdoc />
    public ILog LogWarning() => this;

    /// <inheritdoc />
    public ILog LogError() => this;

    /// <inheritdoc />
    public ILog LogCritical() => this;
}
