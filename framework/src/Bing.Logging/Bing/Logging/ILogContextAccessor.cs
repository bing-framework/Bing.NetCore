namespace Bing.Logging;

/// <summary>
/// 提供当前执行上下文关联的日志上下文。
/// </summary>
public interface ILogContextAccessor
{
    /// <summary>
    /// 获取当前执行上下文的日志上下文。
    /// </summary>
    LogContext Context { get; }
}