using System.Net;
using Bing.Tracing;

namespace Bing.Logging;

/// <summary>
/// 创建并缓存当前执行上下文日志上下文的默认访问器。
/// </summary>
public class LogContextAccessor : ILogContextAccessor
{
    /// <inheritdoc />
    /// <remarks>非 Web 上下文的跟踪标识发生变化时，会创建新的日志上下文以保持跟踪关联一致。</remarks>
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
    /// 创建包含新跟踪标识和当前主机名的日志上下文。
    /// </summary>
    /// <returns>新的日志上下文。</returns>
    protected virtual LogContext Create() => new() { TraceId = GetTraceId(), Host = Dns.GetHostName() };

    /// <summary>
    /// 获取用于新日志上下文的跟踪标识。
    /// </summary>
    /// <returns>新生成的不含分隔符的随机 GUID 标识。</returns>
    protected virtual string GetTraceId() => Guid.NewGuid().ToString("N");
}
