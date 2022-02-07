using System;
using Exceptionless.Logging;
using Serilog.Debugging;

namespace Serilog.Sinks.Exceptionless
{
    /// <summary>
    /// 安全日志记录器
    /// </summary>
    public class SelfLogLogger : IExceptionlessLog
    {
        /// <summary>
        /// 写错误日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="source">来源</param>
        /// <param name="exception">异常</param>
        public void Error(string message, string source = null, Exception exception = null) => SelfLog.WriteLine("Error: {0}, source: {1}, Exception: {2}", message, source, exception);

        /// <summary>
        /// 写信息日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="source">来源</param>
        public void Info(string message, string source = null) => SelfLog.WriteLine("Info: {0}, source: {1}", message, source);

        /// <summary>
        /// 写调试日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="source">来源</param>
        public void Debug(string message, string source = null) => SelfLog.WriteLine("Debug: {0}, source: {1}", message, source);

        /// <summary>
        /// 写警告日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="source">来源</param>
        public void Warn(string message, string source = null) => SelfLog.WriteLine("Warn: {0}, source: {1}", message, source);

        /// <summary>
        /// 写跟踪日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="source">来源</param>
        public void Trace(string message, string source = null) => SelfLog.WriteLine("Trace: {0}, source: {1}", message, source);

        /// <summary>
        /// 刷新
        /// </summary>
        public void Flush() { }

        /// <summary>
        /// 最小日志级别
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; }
    }
}
