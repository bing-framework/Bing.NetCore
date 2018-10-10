namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 任务状态
    /// </summary>
    public enum JobStatus
    {
        /// <summary>
        /// 服务端已经触发
        /// </summary>
        Fire = 0,
        /// <summary>
        /// 客户端收到消息并尝试启动任务
        /// </summary>
        Running = 1,
        /// <summary>
        /// 客户端运行任务成功
        /// </summary>
        Success = 2,
        /// <summary>
        /// 客户端运行任务失败
        /// </summary>
        Failed = 3,
        /// <summary>
        /// 客户端未运行任务
        /// </summary>
        Bypass = 4
    }
}
