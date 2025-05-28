using Bing.Logging.Core;

namespace Bing.Logging;

/// <summary>
/// 日志操作
/// </summary>
/// <typeparam name="TCategoryName">日志类别</typeparam>
public class Log<TCategoryName> : ILog<TCategoryName>
{
    /// <summary>
    /// 日志操作
    /// </summary>
    private readonly ILog _log;

    /// <summary>
    /// 初始化一个<see cref="Log{TCategoryName}"/>类型的实例
    /// </summary>
    /// <param name="factory">日志操作工厂</param>
    public Log(ILogFactory factory)
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));
        _log = factory.CreateLog(typeof(TCategoryName));
    }

    /// <summary>
    /// 空日志操作实例
    /// </summary>
    public static ILog<TCategoryName> Null = NullLog<TCategoryName>.Instance;

    /// <inheritdoc />
    public virtual ILog EventId(EventId eventId) => _log.EventId(eventId);

    /// <inheritdoc />
    public virtual ILog Exception(Exception exception) => _log.Exception(exception);

    /// <inheritdoc />
    public virtual ILog Property(string propertyName, string propertyValue) => _log.Property(propertyName, propertyValue);

    /// <inheritdoc />
    public virtual ILog Set(Action<LogEventDescriptor> action) => _log.Set(action);

    /// <inheritdoc />
    public virtual ILog State(object state) => _log.State(state);

    /// <inheritdoc />
    public virtual ILog Message(string message, params object[] args) => _log.Message(message, args);

    /// <inheritdoc />
    public virtual bool IsEnabled(LogLevel logLevel) => _log.IsEnabled(logLevel);

    /// <inheritdoc />
    public virtual IDisposable BeginScope<TState>(TState state) => _log.BeginScope(state);

    /// <inheritdoc />
    public virtual ILog LogTrace() => _log.LogTrace();

    /// <inheritdoc />
    public virtual ILog LogDebug() => _log.LogDebug();

    /// <inheritdoc />
    public virtual ILog LogInformation() => _log.LogInformation();

    /// <inheritdoc />
    public virtual ILog LogWarning() => _log.LogWarning();

    /// <inheritdoc />
    public virtual ILog LogError() => _log.LogError();

    /// <inheritdoc />
    public virtual ILog LogCritical() => _log.LogCritical();
}
