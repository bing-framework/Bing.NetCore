using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Events.Ordering;

/// <summary>
/// 提供单个有序逻辑消费者的运行状态和排空控制。
/// </summary>
/// <remarks>
/// 该契约用于部署切换和运维协调。请求排空只停止继续扫描，不会强制终止已经进入处理器的业务；
/// 宿主停止令牌取消时，实际清理仍由消费者和数据库事务决定。
/// </remarks>
public interface IOrderedEventConsumerRuntime
{
    /// <summary>
    /// 获取跨版本保持稳定的逻辑消费者名称。
    /// </summary>
    string ConsumerName { get; }

    /// <summary>
    /// 获取当前是否已经启动且尚未退出。
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 获取当前是否已经请求排空。
    /// </summary>
    bool IsDraining { get; }

    /// <summary>
    /// 获取当前仍在处理的业务流数量。
    /// </summary>
    long InFlightCount { get; }

    /// <summary>
    /// 请求停止新的扫描并排空已经开始的处理。
    /// </summary>
    void BeginDrain();

    /// <summary>
    /// 等待消费者实际退出。
    /// </summary>
    /// <param name="timeout">最长等待时间；使用 <see cref="Timeout.InfiniteTimeSpan"/> 表示不设超时。</param>
    /// <param name="cancellationToken">仅取消当前等待，不强制释放活动处理。</param>
    /// <returns>在超时或取消前已经退出时返回 <see langword="true"/>。</returns>
    Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
