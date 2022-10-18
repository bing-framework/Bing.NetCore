using System;
using Bing.Logs;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// 系统日志记录器
    /// </summary>
    internal class SysLogger : ILogger
    {
        /// <summary>
        /// 日志名称
        /// </summary>
        public const string LogName = "SysLog";

        /// <summary>
        /// 日志分类
        /// </summary>
        protected string CategoryName { get; }

        /// <summary>
        /// 初始化一个<see cref="SysLogger"/>类型的实例
        /// </summary>
        /// <param name="categoryName">日志分类</param>
        public SysLogger(string categoryName) => CategoryName = categoryName;

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="logLevel">日志级别</param>
        /// <param name="eventId">事件编号</param>
        /// <param name="state">状态</param>
        /// <param name="exception">异常</param>
        /// <param name="formatter">日志内容</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));
            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
                return;
            var log = Bing.Logs.Log.GetLog(CategoryName);
            log
                .Tag(LogName)
                .Tag(CategoryName)
                .Caption($"系统日志：{message}")
                .Content($"事件ID：{eventId.Id}")
                .Content($"事件名称：{eventId.Name}")
                .Content(message);
            if (exception != null)
                log.Exception(exception);
            switch (logLevel)
            {
                case LogLevel.Trace:
                    log.Trace();
                    break;
                case LogLevel.Debug:
                    log.Debug();
                    break;
                case LogLevel.Information:
                    log.Info();
                    break;
                case LogLevel.Warning:
                    log.Warn();
                    break;
                case LogLevel.Error:
                    log.Error();
                    break;
                case LogLevel.Critical:
                    log.Fatal();
                    break;
            }
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>
        /// 起始范围
        /// </summary>
        public IDisposable BeginScope<TState>(TState state) => null;
    }
}
