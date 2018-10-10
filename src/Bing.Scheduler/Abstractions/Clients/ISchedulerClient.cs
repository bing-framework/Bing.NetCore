namespace Bing.Scheduler.Abstractions.Clients
{
    /// <summary>
    /// 任务调度客户端
    /// </summary>
    public interface ISchedulerClient
    {
        /// <summary>
        /// 初始化并启动
        /// </summary>
        void Init();
    }
}
