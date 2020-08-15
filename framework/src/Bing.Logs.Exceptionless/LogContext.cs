using System;
using Bing.DependencyInjection;
using Bing.Helpers;
using Bing.Logs.Core;
using Bing.Logs.Internal;

namespace Bing.Logs.Exceptionless
{
    /// <summary>
    /// Exceptionless日志上下文
    /// </summary>
    public class LogContext : Logs.Core.LogContext
    {
        /// <summary>
        /// 初始化一个<see cref="Core.LogContext"/>类型的实例
        /// </summary>
        /// <param name="scopedDictionary">作用域字典</param>
        public LogContext(ScopedDictionary scopedDictionary) : base(scopedDictionary)
        {
        }

        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        protected override LogContextInfo CreateInfo() =>
            new LogContextInfo
            {
                TraceId = Guid.NewGuid().ToString(),
                Stopwatch = GetStopwatch(),
                Url = Web.Url
            };
    }
}
