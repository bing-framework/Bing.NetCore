using System;
using System.Collections.Generic;
using System.Text;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Text;
using Microsoft.Extensions.Logging;

namespace Bing.Logging
{
    /// <summary>
    /// 日志操作
    /// </summary>
    /// <typeparam name="TCategoryName">日志类别</typeparam>
    public class Log<TCategoryName> : ILog<TCategoryName>
    {
        /// <summary>
        /// 初始化一个<see cref="Log{TCategoryName}"/>类型的实例
        /// </summary>
        /// <param name="logger">日志记录包装器</param>
        /// <param name="logContextAccessor">日志上下文访问器</param>
        public Log(ILoggerWrapper<TCategoryName> logger, ILogContextAccessor logContextAccessor = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LogContext = logContextAccessor?.Context;
            LogProperties = new Dictionary<string, object>();
            LogMessage = new StringBuilder();
            LogMessageArgs = new List<object>();
        }

        /// <summary>
        /// 日志记录包装器
        /// </summary>
        protected ILoggerWrapper<TCategoryName> Logger { get; }

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
        protected IDictionary<string, object> LogProperties { get; set; }

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

        /// <inheritdoc />
        public virtual ILog<TCategoryName> EventId(EventId eventId)
        {
            LogEventId = eventId;
            return this;
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> Exception(Exception exception)
        {
            LogException = exception;
            return this;
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> Property(string propertyName, string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return this;
            if (LogProperties.ContainsKey(propertyName))
            {
                LogProperties[propertyName] += propertyValue;
                return this;
            }

            LogProperties.Add(propertyName, propertyValue);
            return this;
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> State(object state)
        {
            LogState = state;
            return this;
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> Message(string message, params object[] args)
        {
            LogMessage.Append(message);
            LogMessageArgs.AddRange(args);
            return this;
        }

        /// <inheritdoc />
        public virtual bool IsEnabled(LogLevel logLevel) => Logger.IsEnabled(logLevel);

        /// <inheritdoc />
        public virtual IDisposable BeginScope<TState>(TState state) => Logger.BeginScope(state);

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogTrace()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogTrace(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Trace;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogDebug()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogDebug(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Debug;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogInformation()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogInformation(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Information;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogWarning()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogWarning(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Warning;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogError()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogError(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Error;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

        /// <inheritdoc />
        public virtual ILog<TCategoryName> LogCritical()
        {
            try
            {
                Init();
                if (LogMessage.Length > 0)
                {
                    Logger.LogCritical(LogEventId, LogException, GetMessage(), GetMessageArgs());
                    return this;
                }

                LogLevel = LogLevel.Critical;
                return WriteLog();
            }
            finally
            {
                Clear();
            }
        }

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
            Property("TraceId", LogContext.TraceId);
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
                if(item.Value.SafeString().IsEmpty())
                    continue;
                LogProperties.Add(item);
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
        protected virtual ILog<TCategoryName> WriteLog()
        {
            Logger.Log(LogLevel, LogEventId, GetContent(), LogException, GetFormatMessage);
            return this;
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
            LogProperties = new Dictionary<string, object>();
            LogMessage.Clear();
            LogMessageArgs.Clear();
        }

        #endregion


    }
}
