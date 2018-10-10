using Bing.Scheduler.Abstractions.Clients;

namespace Bing.Scheduler.Core.Clients
{
    /// <summary>
    /// 简单任务处理器
    /// </summary>
    public abstract class SimpleJobProcessor:IJobProcessor
    {
        /// <summary>
        /// 处理任务
        /// </summary>
        /// <param name="context">任务上下文</param>
        /// <returns></returns>
        public abstract bool Process(JobContext context);
    }
}
