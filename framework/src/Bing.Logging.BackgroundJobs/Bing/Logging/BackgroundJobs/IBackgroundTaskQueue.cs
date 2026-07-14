namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 后台任务队列
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// 将任务加入队列
    /// </summary>
    /// <param name="task">后台任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask QueueAsync(BackgroundTaskDelegate task, CancellationToken cancellationToken = default);
}