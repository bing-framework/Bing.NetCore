namespace Bing.Logging.BackgroundJobs;

/// <summary>
/// 后台任务队列配置
/// </summary>
public sealed class BackgroundTaskQueueOptions
{
    /// <summary>
    /// 队列容量
    /// </summary>
    public int Capacity { get; set; } = 100;
}