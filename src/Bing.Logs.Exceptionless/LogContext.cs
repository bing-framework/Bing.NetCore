using System;
using Bing.Helpers;
using Bing.Logs.Internal;

namespace Bing.Logs.Exceptionless
{
    /// <summary>
    /// Exceptionless日志上下文
    /// </summary>
    public class LogContext : Logs.Core.LogContext
    {
        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        /// <returns></returns>
        protected override LogContextInfo CreateInfo()
        {
            return new LogContextInfo()
            {
                TraceId = Guid.NewGuid().ToString(),
                Stopwatch = GetStopwatch(),
                Url = Web.Url
            };
        }
    }
}
