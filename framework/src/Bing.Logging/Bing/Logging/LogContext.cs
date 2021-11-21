using System.Diagnostics;

namespace Bing.Logging
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public class LogContext
    {
        /// <summary>
        /// 初始化一个<see cref="LogContext"/>类型的实例
        /// </summary>
        /// <param name="stopwatch">计时器</param>
        /// <param name="traceId">跟踪标识</param>
        public LogContext(Stopwatch stopwatch, string traceId)
        {
            Stopwatch = stopwatch;
            TraceId = traceId;
        }

        /// <summary>
        /// 计时器
        /// </summary>
        public Stopwatch Stopwatch { get; }

        /// <summary>
        /// 跟踪标识
        /// </summary>
        public string TraceId { get; }
    }
}
