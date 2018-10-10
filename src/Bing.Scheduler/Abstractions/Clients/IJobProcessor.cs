using Bing.Scheduler.Core;

namespace Bing.Scheduler.Abstractions.Clients
{
    /// <summary>
    /// 任务处理器
    /// </summary>
    public interface IJobProcessor
    {
        /// <summary>
        /// 处理任务
        /// </summary>
        /// <param name="context">任务上下文</param>
        /// <returns></returns>
        bool Process(JobContext context);
    }
}
