using System.Runtime.ExceptionServices;
using Bing.Logging;
using Microsoft.Extensions.Logging;

namespace System
{
    /// <summary>
    /// 异常(<see cref="Exception"/>) 扩展
    /// </summary>
    public static class BingExceptionExtensions
    {
        /// <summary>
        /// 重新抛出异常，同时保留堆栈跟踪。使用 <see cref="ExceptionDispatchInfo.Capture"/> 方法重新抛出异常
        /// </summary>
        /// <param name="exception">异常</param>
        public static void ReThrow(this Exception exception) => ExceptionDispatchInfo.Capture(exception).Throw();

        /// <summary>
        /// 获取日志级别
        /// </summary>
        /// <param name="exception">异常</param>
        /// <param name="defaultLevel">默认日志级别</param>
        public static LogLevel GetLogLevel(this Exception exception, LogLevel defaultLevel = LogLevel.Error) => (exception as IHasLogLevel)?.LogLevel ?? defaultLevel;
    }
}
