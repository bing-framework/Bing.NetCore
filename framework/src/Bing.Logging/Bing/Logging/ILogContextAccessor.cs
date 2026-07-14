namespace Bing.Logging;

/// <summary>
/// 日志上下文访问器
/// </summary>
public interface ILogContextAccessor
{
    /// <summary>
    /// 当前日志上下文快照
    /// </summary>
    LogContextSnapshot Current { get; }

    /// <summary>
    /// 捕获当前日志上下文快照
    /// </summary>
    LogContextSnapshot Capture();

    /// <summary>
    /// 应用日志上下文快照，并在释放时恢复父级上下文
    /// </summary>
    /// <param name="snapshot">日志上下文快照</param>
    IDisposable BeginScope(LogContextSnapshot snapshot);
}