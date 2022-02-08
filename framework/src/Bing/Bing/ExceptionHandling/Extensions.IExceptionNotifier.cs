using System;
using System.Threading.Tasks;
using Bing.Helpers;
using Microsoft.Extensions.Logging;

namespace Bing.ExceptionHandling
{
    /// <summary>
    /// 异常通知器(<see cref="IExceptionNotifier"/>) 扩展
    /// </summary>
    public static class ExceptionNotifierExtensions
    {
        /// <summary>
        /// 通知
        /// </summary>
        /// <param name="exceptionNotifier">异常通知器</param>
        /// <param name="exception">异常对象</param>
        /// <param name="logLevel">日志级别</param>
        /// <param name="handled">是否已处理异常</param>
        public static Task NotifyAsync(this IExceptionNotifier exceptionNotifier, Exception exception, LogLevel? logLevel = null, bool handled = true)
        {
            Check.NotNull(exceptionNotifier, nameof(exceptionNotifier));
            return exceptionNotifier.NotifyAsync(new ExceptionNotificationContext(exception, logLevel, handled));
        }
    }
}
