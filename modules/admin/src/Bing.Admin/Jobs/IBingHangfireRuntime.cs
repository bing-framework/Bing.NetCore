using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Admin.Jobs;

/// <summary>
/// 提供 Hangfire 任务排空状态。
/// </summary>
public interface IBingHangfireRuntime
{
    /// <summary>
    /// 获取是否已停止接收新任务。
    /// </summary>
    bool IsDraining { get; }

    /// <summary>
    /// 获取当前正在执行的任务数。
    /// </summary>
    int InFlightCount { get; }

    /// <summary>
    /// 停止接受新的任务执行。
    /// </summary>
    void BeginDrain();

    /// <summary>
    /// 等待已接收的任务执行完成。
    /// </summary>
    /// <param name="timeout">等待超时时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>在超时前完成排空时返回 <see langword="true"/>。</returns>
    Task<bool> WaitForDrainAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
