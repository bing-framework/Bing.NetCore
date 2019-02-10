using System.Reflection;
using System.Threading.Tasks;

namespace Bing.Schedulers
{
    /// <summary>
    /// 调度器
    /// </summary>
    public interface IScheduler
    {
        /// <summary>
        /// 启动
        /// </summary>
        /// <returns></returns>
        Task StartAsync();

        /// <summary>
        /// 暂停
        /// </summary>
        /// <returns></returns>
        Task PauseAsync();

        /// <summary>
        /// 恢复
        /// </summary>
        /// <returns></returns>
        Task ResumeAsync();

        /// <summary>
        /// 停止
        /// </summary>
        /// <returns></returns>
        Task StopAsync();

        /// <summary>
        /// 添加作业
        /// </summary>
        /// <typeparam name="TJob">作业类型</typeparam>
        /// <returns></returns>
        Task AddJobAsync<TJob>() where TJob : IJob, new();

        /// <summary>
        /// 扫描并添加作业
        /// </summary>
        /// <param name="assemblies">程序集列表</param>
        /// <returns></returns>
        Task ScanJobAsync(params Assembly[] assemblies);
    }
}
