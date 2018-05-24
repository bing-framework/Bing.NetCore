using System;
using System.Threading;
using Bing.Logs.Abstractions;
using Bing.Logs.Extensions;
using Bing.Sessions;
using Bing.Utils.Extensions;

namespace Bing.Logs.Core
{
    /// <summary>
    /// 日志操作抽象基类
    /// </summary>
    /// <typeparam name="TContent">日志内容类型</typeparam>
    public abstract class LogBase<TContent> : ILog where TContent : class, ILogContent
    {
        #region 属性
        /// <summary>
        /// 日志内容
        /// </summary>
        private TContent _content;

        /// <summary>
        /// 日志内容
        /// </summary>
        private TContent LogContent => _content ?? (_content = GetContent());

        /// <summary>
        /// 日志提供程序
        /// </summary>
        public ILogProvider Provider { get; }

        /// <summary>
        /// 日志上下文
        /// </summary>
        public ILogContext Context { get; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }

        /// <summary>
        /// 调试级别是否启用
        /// </summary>
        public bool IsDebugEnabled => Provider.IsDebugEnabled;

        /// <summary>
        /// 跟踪级别是否启用
        /// </summary>
        public bool IsTraceEnabled => Provider.IsTraceEnabled;
        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="LogBase{TContent}"/>类型的实例
        /// </summary>
        /// <param name="provider">日志提供程序</param>
        /// <param name="context">日志上下文</param>
        /// <param name="session">用户会话</param>
        protected LogBase(ILogProvider provider, ILogContext context, ISession session)
        {
            Provider = provider;
            Context = context;
            Session = session ?? NullSession.Instance;
        }
        #endregion

        /// <summary>
        /// 获取日志内容
        /// </summary>
        /// <returns></returns>
        protected abstract TContent GetContent();

        /// <summary>
        /// 设置内容
        /// </summary>
        /// <typeparam name="T">日志内容类型</typeparam>
        /// <param name="action">设置内容操作</param>
        /// <returns></returns>
        public ILog Set<T>(Action<T> action) where T : ILogContent
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
            ILogContent content = LogContent;
            action((T)content);
            return this;
        }

        /// <summary>
        /// 获取内容
        /// </summary>
        /// <typeparam name="T">日志内容类型</typeparam>
        /// <returns></returns>
        public T Get<T>() where T : ILogContent
        {
            ILogContent content = LogContent;
            return (T)content;
        }

        /// <summary>
        /// 跟踪
        /// </summary>
        public void Trace()
        {
            _content = LogContent;
            Execute(LogLevel.Trace, ref _content);
        }

        /// <summary>
        /// 跟踪
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Trace(string message)
        {
            LogContent.Content(message);
            Trace();
        }

        /// <summary>
        /// 调试
        /// </summary>
        public void Debug()
        {
            _content = LogContent;
            Execute(LogLevel.Debug, ref _content);
        }

        /// <summary>
        /// 调试
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Debug(string message)
        {
            LogContent.Content(message);
            Debug();
        }

        /// <summary>
        /// 信息
        /// </summary>
        public void Info()
        {
            _content = LogContent;
            Execute(LogLevel.Information, ref _content);
        }

        /// <summary>
        /// 信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message)
        {
            LogContent.Content(message);
            Info();
        }

        /// <summary>
        /// 警告
        /// </summary>
        public void Warn()
        {
            _content = LogContent;
            Execute(LogLevel.Warning, ref _content);
        }

        /// <summary>
        /// 警告
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warn(string message)
        {
            LogContent.Content(message);
            Warn();
        }

        /// <summary>
        /// 错误
        /// </summary>
        public void Error()
        {
            _content = LogContent;
            Execute(LogLevel.Error, ref _content);
        }

        /// <summary>
        /// 错误
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Error(string message)
        {
            LogContent.Content(message);
            Error();
        }

        /// <summary>
        /// 致命错误
        /// </summary>
        public void Fatal()
        {
            _content = LogContent;
            Execute(LogLevel.Fatal, ref _content);
        }

        /// <summary>
        /// 致命错误
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Fatal(string message)
        {
            LogContent.Content(message);
            Fatal();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="content">日志内容</param>
        protected virtual void Init(TContent content)
        {
            content.LogName = Provider.LogName;
            content.TraceId = Context.TraceId;
            content.OperationTime = DateTime.Now.ToMillisecondString();
            content.Duration = Context.Stopwatch.Elapsed.Description();
            content.Ip = Context.Ip;
            content.Host = Context.Host;
            content.ThreadId = Thread.CurrentThread.ManagedThreadId.ToString();
            content.Browser = Context.Browser;
            content.Url = Context.Url;
            content.UserId = Session.UserId;
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="content">日志内容</param>
        protected virtual void Execute(LogLevel level, ref TContent content)
        {
            if (content == null)
            {
                return;
            }
            if (Enabled(level) == false)
            {
                return;
            }
            try
            {
                content.Level = Utils.Helpers.Enum.GetName<LogLevel>(level);
                Init(content);
                Provider.WriteLog(level, content);
            }
            finally
            {
                content = null;
            }
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        private bool Enabled(LogLevel level)
        {
            if (level >= LogLevel.Debug)
            {
                return true;
            }
            return IsDebugEnabled || IsTraceEnabled && level == LogLevel.Trace;
        }
    }
}
