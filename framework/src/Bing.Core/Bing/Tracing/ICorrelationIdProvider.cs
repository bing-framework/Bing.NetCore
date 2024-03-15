namespace Bing.Tracing;

/// <summary>
/// 跟踪标识提供程序
/// </summary>
/// <remarks>
/// 提供了一种机制来管理和传递跨异步流程的关联ID（Correlation ID）。
/// </remarks>
public interface ICorrelationIdProvider
{
    /// <summary>
    /// 获取当前关联ID的值。
    /// </summary>
    string Get();

    /// <summary>
    /// 更改当前的关联ID，并在返回的 <see cref="IDisposable"/> 对象被释放时恢复之前的关联ID。
    /// </summary>
    /// <param name="correlationId">关联ID</param>
    /// <returns><see cref="IDisposable"/> 对象，在被释放时恢复之前的关联ID。</returns>
    IDisposable Change(string correlationId);
}
