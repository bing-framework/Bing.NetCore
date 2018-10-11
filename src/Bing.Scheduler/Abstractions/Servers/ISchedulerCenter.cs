using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Scheduler.Core;
using Bing.Scheduler.Core.Models;

namespace Bing.Scheduler.Abstractions.Servers
{
    /// <summary>
    /// 调度中心
    /// </summary>
    public interface ISchedulerCenter
    {
        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="entity">调度信息</param>
        /// <returns></returns>
        Task<Result> AddJobAsync(ScheduleInfo entity);

        /// <summary>
        /// 暂停/删除 指定的计划
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <param name="isDelete">停止并删除任务</param>
        /// <returns></returns>
        Task<Result> StopOrDeleteJobAsync(string jobGroup, string jobName, bool isDelete = false);

        /// <summary>
        /// 恢复暂停中的任务
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <returns></returns>
        Task<Result> ResumeJobAsync(string jobGroup, string jobName);

        /// <summary>
        /// 查询任务
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <returns></returns>
        Task<ScheduleInfo> QueryJobAsync(string jobGroup, string jobName);

        /// <summary>
        /// 立即执行
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <returns></returns>
        Task<bool> TriggerJobAsync(string jobGroup, string jobName);

        /// <summary>
        /// 获取任务日志
        /// </summary>
        /// <param name="jobGroup">任务分组</param>
        /// <param name="jobName">任务名称</param>
        /// <returns></returns>
        Task<List<string>> GetJobLogAsync(string jobGroup, string jobName);

        /// <summary>
        /// 获取所有任务（详细信息 - 初始化页面调用）
        /// </summary>
        /// <returns></returns>
        Task<List<JobInfo>> GetAllJobAsync();

        /// <summary>
        /// 获取所有任务（简要信息 - 刷新数据的时候使用）
        /// </summary>
        /// <returns></returns>
        Task<List<JobInfoBrief>> GetAllJobBriefAsync();

        /// <summary>
        /// 启动调度
        /// </summary>
        /// <returns></returns>
        Task<bool> StartAsync();

        /// <summary>
        /// 停止调度
        /// </summary>
        /// <returns></returns>
        Task<bool> StopAsync();
    }
}
