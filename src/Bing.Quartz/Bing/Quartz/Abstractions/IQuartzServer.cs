using System.Threading;
using System.Threading.Tasks;
using Quartz;

namespace Bing.Quartz.Abstractions
{
    /// <summary>
    /// Quartz服务
    /// </summary>
    public interface IQuartzServer
    {
        /// <summary>
        /// 启动
        /// </summary>
        /// <param name="cancellation">取消令牌</param>
        Task StartAsync(CancellationToken cancellation = default);

        /// <summary>
        /// 停止
        /// </summary>
        /// <param name="cancellation">取消令牌</param>
        Task StopAsync(CancellationToken cancellation = default);

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="jobDetail">任务详情</param>
        /// <param name="trigger">触发器</param>
        /// <param name="cancellation">取消令牌</param>
        Task AddJobAsync(IJobDetail jobDetail, ITrigger trigger, CancellationToken cancellation = default);

        /// <summary>
        /// 暂停任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        Task PauseJobAsync(JobKey jobKey, CancellationToken cancellation = default);

        /// <summary>
        /// 恢复任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        Task ResumeJobAsync(JobKey jobKey, CancellationToken cancellation = default);

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="jobKey">任务键</param>
        /// <param name="cancellation">取消令牌</param>
        Task DeleteJobAsync(JobKey jobKey, CancellationToken cancellation = default);
    }
}
