using System.Diagnostics;
using System.Net;
using Bing.Tracing;

namespace Bing.Logging;

/// <summary>
/// 日志上下文访问器
/// </summary>
public class LogContextAccessor : ILogContextAccessor
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public LogContext Context
    {
        get
        {
            var current = LogContext.Current;
            if (current != null)
            {
                if (!current.IsWebEnv && current.TraceId != TraceIdContext.Current?.TraceId)
                    return LogContext.Current = Create();
                return current;
            }
            return LogContext.Current = Create();
        }
    }

    /// <summary>
    /// 创建日志上下文
    /// </summary>
    protected virtual LogContext Create() => new() { TraceId = GetTraceId(), Host = Dns.GetHostName() };

    /// <summary>
    /// 获取跟踪标识
    /// </summary>
    protected virtual string GetTraceId() => Guid.NewGuid().ToString("N");
}
