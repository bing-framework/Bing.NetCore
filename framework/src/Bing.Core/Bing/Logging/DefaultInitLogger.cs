using Microsoft.Extensions.Logging;

namespace Bing.Logging;

/// <summary>
/// 初始化日志记录器
/// </summary>
/// <typeparam name="T">类型</typeparam>
public class DefaultInitLogger<T> : IInitLogger<T>
{
    /// <summary>
    /// 初始化一个<see cref="DefaultInitLogger{T}"/>类型的实例
    /// </summary>
    public DefaultInitLogger()
    {
        Entries = new List<BingInitLogEntry>();
    }

    /// <summary>
    /// 条目列表
    /// </summary>
    public List<BingInitLogEntry> Entries { get; }

    /// <inheritdoc />
    public virtual void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        Entries.Add(new BingInitLogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            State = state,
            Exception = exception,
            Formatter = (s, e) => formatter((TState)s, e)
        });
    }

    /// <inheritdoc />
    /// <returns>日志级别不为 <see cref="LogLevel.None"/> 时返回 <see langword="true"/>。</returns>
    public virtual bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    /// <returns>用于释放日志作用域的对象。</returns>
    public virtual IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;
}
