using System.Collections.Concurrent;
using Bing.Collections;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logging.Core;
using Bing.Text;

namespace Bing.Logging;

/// <summary>
/// 日志操作
/// </summary>
public class Log : ILog
{
    #region 字段

    /// <summary>
    /// 当前的日志事件描述符
    /// </summary>
    private LogEventDescriptor CurrentDescriptor { get; set; }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="Log"/>类型的实例
    /// </summary>
    /// <param name="logger">日志记录包装器</param>
    /// <param name="logContextAccessor">日志上下文访问器</param>
    public Log(ILoggerWrapper logger, ILogContextAccessor logContextAccessor = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LogContext = logContextAccessor?.Context;
        LogProperties = new ConcurrentDictionary<string, object>();
        LogMessage = new StringBuilder();
        LogMessageArgs = new List<object>();
        CurrentDescriptor = new LogEventDescriptor();
    }

    #endregion

    #region Null(空日志操作实例)

    /// <summary>
    /// 空日志操作实例
    /// </summary>
    public static ILog Null = NullLog.Instance;

    #endregion

    #region 属性

    /// <summary>
    /// 日志记录包装器
    /// </summary>
    protected ILoggerWrapper Logger { get; }

    /// <summary>
    /// 日志上下文
    /// </summary>
    protected LogContext LogContext { get; }

    /// <summary>
    /// 日志级别
    /// </summary>
    protected LogLevel LogLevel { get; set; }

    /// <summary>
    /// 日志事件标识
    /// </summary>
    protected EventId LogEventId { get; set; }

    /// <summary>
    /// 日志异常
    /// </summary>
    protected Exception LogException { get; set; }

    /// <summary>
    /// 日志内容
    /// </summary>
    protected ConcurrentDictionary<string, object> LogProperties { get; set; }

    /// <summary>
    /// 日志状态
    /// </summary>
    protected object LogState { get; set; }

    /// <summary>
    /// 日志消息
    /// </summary>
    protected StringBuilder LogMessage { get; }

    /// <summary>
    /// 日志消息参数
    /// </summary>
    protected List<object> LogMessageArgs { get; }

    #endregion

    #region EventId(设置日志事件标识)

    /// <inheritdoc />
    public virtual ILog EventId(EventId eventId)
    {
        LogEventId = eventId;
        return this;
    }

    #endregion

    #region Exception(设置异常)

    /// <inheritdoc />
    public virtual ILog Exception(Exception exception)
    {
        LogException = exception;
        return this;
    }

    #endregion

    #region Property(设置自定义扩展属性)

    /// <inheritdoc />
    public virtual ILog Property(string propertyName, string propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return this;
        if (LogProperties.ContainsKey(propertyName))
        {
            LogProperties[propertyName] += propertyValue;
            return this;
        }
        LogProperties.TryAdd(propertyName, propertyValue);
        return this;
    }

    #endregion

    #region Set(设置日志事件描述符)

    /// <summary>
    /// 设置日志事件描述符
    /// </summary>
    /// <param name="action">操作</param>
    public ILog Set(Action<LogEventDescriptor> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        action(CurrentDescriptor);
        return this;
    }

    #endregion

    #region State(设置日志状态对象)

    /// <inheritdoc />
    public virtual ILog State(object state)
    {
        LogState = state;
        return this;
    }

    #endregion

    #region Message(设置日志消息)

    /// <inheritdoc />
    public virtual ILog Message(string message, params object[] args)
    {
        LogMessage.Append(message);
        LogMessageArgs.AddRange(args);
        return this;
    }

    #endregion

    #region IsEnabled(是否启用)

    /// <inheritdoc />
    public virtual bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

    #endregion

    #region BeginScope(开始日志范围)

    /// <inheritdoc />
    public virtual IDisposable BeginScope<TState>(TState state) => Logger.BeginScope(state);

    #endregion

    #region LogTrace(写跟踪日志)

    /// <inheritdoc />
    public virtual ILog LogTrace([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Trace, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region LogDebug(写调试日志)

    /// <inheritdoc />
    public virtual ILog LogDebug([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Debug, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region LogInformation(写信息日志)

    /// <inheritdoc />
    public virtual ILog LogInformation([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Information, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region LogWarning(写警告日志)

    /// <inheritdoc />
    public virtual ILog LogWarning([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Warning, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region LogError(写错误日志)

    /// <inheritdoc />
    public virtual ILog LogError([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Error, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region LogCritical(写致命日志)

    /// <inheritdoc />
    public virtual ILog LogCritical([CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0) => WriteLog(LogLevel.Critical, memberName, sourceFilePath, sourceLineNumber);

    #endregion

    #region 辅助方法

    /// <summary>
    /// 初始化
    /// </summary>
    protected virtual void Init()
    {
        AddLogContext();
        ConvertStateToContent();
    }

    /// <summary>
    /// 添加日志上下文
    /// </summary>
    protected virtual void AddLogContext()
    {
        if (LogContext == null)
            return;
        if (!string.IsNullOrWhiteSpace(LogContext.TraceId))
            Property("TraceId", LogContext.TraceId);
        if (LogContext.Stopwatch != null)
            Property("Duration", LogContext.Stopwatch.Elapsed.Description());
    }

    /// <summary>
    /// 将状态对象转换到日志内容字典中
    /// </summary>
    protected virtual void ConvertStateToContent()
    {
        if (LogState == null)
            return;
        var state = Conv.ToDictionary(LogState);
        foreach (var item in state)
        {
            if (item.Value.SafeString().IsEmpty())
                continue;
            LogProperties.TryAdd(item.Key,item.Value);
        }
    }

    /// <summary>
    /// 获取日志消息
    /// </summary>
    protected virtual string GetMessage()
    {
        if (LogProperties.Count == 0)
            return LogMessage.ToString();
        var result = new StringBuilder();
        result.Append("[");
        foreach (var item in LogProperties)
        {
            result.Append(item.Key);
            result.Append(":{");
            result.Append(item.Key);
            result.Append("},");
        }

        result.TrimEnd(',');
        result.Append("]");
        result.Append(LogMessage);
        return result.ToString();
    }

    /// <summary>
    /// 获取日志消息参数
    /// </summary>
    protected virtual object[] GetMessageArgs()
    {
        if (LogProperties.Count == 0)
            return LogMessageArgs.ToArray();
        var result = new List<object>();
        result.AddRange(LogProperties.Values);
        result.AddRange(LogMessageArgs);
        return result.ToArray();
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="memberName">方法名</param>
    /// <param name="sourceFilePath">文件路径</param>
    /// <param name="sourceLineNumber">行号</param>
    protected virtual ILog WriteLog(LogLevel level, string memberName, string sourceFilePath, int sourceLineNumber)
    {
        try
        {
            LogLevel = level;
            CurrentDescriptor.Context.SetCallerInfo(memberName, sourceFilePath, sourceLineNumber);
            var scopeDict = CurrentDescriptor.Context.ExposeScopeState();
            using (Logger.BeginScope(scopeDict))
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.Log(level, LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                Logger.Log(LogLevel, LogEventId, GetContent(), LogException, GetFormatMessage);
                return this;
            }
        }
        finally
        {
            Clear();
        }
    }

    /// <summary>
    /// 获取日志内容
    /// </summary>
    protected IDictionary<string, object> GetContent() => LogProperties.Count == 0 ? null : LogProperties;

    /// <summary>
    /// 获取格式化消息
    /// </summary>
    /// <param name="content">日志内容</param>
    /// <param name="exception">异常</param>
    protected virtual string GetFormatMessage(IDictionary<string, object> content, Exception exception)
    {
        if (exception != null)
            return exception.Message;
        if (content == null)
            return null;
        return GetFormatMessage(content);
    }

    /// <summary>
    /// 获取格式化消息
    /// </summary>
    /// <param name="content">日志内容</param>
    protected virtual string GetFormatMessage(IDictionary<string, object> content)
    {
        var result = new StringBuilder();
        foreach (var item in content)
        {
            result.Append(item.Key);
            result.Append(":");
            result.Append(item.Value.SafeString());
            result.Append(",");
        }

        return result.ToString().TrimEnd(',');
    }

    /// <summary>
    /// 清理状态
    /// </summary>
    protected virtual void Clear()
    {
        LogLevel = LogLevel.None;
        LogEventId = 0;
        LogException = null;
        LogState = null;
        LogProperties = new ConcurrentDictionary<string, object>();
        LogMessage.Clear();
        LogMessageArgs.Clear();
        CurrentDescriptor = new LogEventDescriptor();
    }

    #endregion
}
