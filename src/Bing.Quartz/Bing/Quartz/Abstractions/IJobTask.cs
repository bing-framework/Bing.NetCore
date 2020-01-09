using System.Threading.Tasks;
using Quartz;

namespace Bing.Quartz.Abstractions
{
    /// <summary>
    /// 任务
    /// </summary>
    public interface IJobTask : IJob
    {
        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context">任务上下文</param>
        Task Execute(IJobTaskContext context);
    }
}
