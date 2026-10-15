using System.Threading;
using System.Threading.Tasks;

namespace Bing.Events.Cap;

/// <summary>
/// 提供 CAP 消费回调的排空状态和控制能力。
/// </summary>
/// <remarks>
/// 该契约只覆盖通过 <see cref="BingSubscribeInvoker"/> 进入的回调。
/// 请求排空不会强制终止已经运行的业务，也不会替代 CAP 原生处理器的停止流程。
/// </remarks>
public interface IBingCapConsumerRuntime
{
    /// <summary>
    /// 获取是否已经请求停止接收新的消费回调。
    /// </summary>
    bool IsDraining { get; }

    /// <summary>
    /// 获取当前仍在执行的消费回调数量。
    /// </summary>
    int InFlightCount { get; }

    /// <summary>
    /// 请求停止新的消费回调并等待当前回调排空。
    /// </summary>
    void BeginDrain();

    /// <summary>
    /// 等待当前消费回调实际退出。
    /// </summary>
    /// <param name="timeout">最长等待时间；使用 <see cref="Timeout.InfiniteTimeSpan"/> 表示不设超时。</param>
    /// <param name="cancellationToken">仅取消当前等待，不强制释放活动回调。</param>
    /// <returns>在超时或取消前全部回调已经退出时返回 <see langword="true"/>。</returns>
    Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
