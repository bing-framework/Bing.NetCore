using Bing.Scheduler.Core;

namespace Bing.Scheduler.Abstractions.Clients
{
    /// <summary>
    /// 任务调度管理器
    /// </summary>
    public interface ISchedulerManager
    {
        /// <summary>
        /// 服务鉴权的 Header 名称
        /// </summary>
        string TokenHeader { get; set; }

        /// <summary>
        /// 创建普通任务
        /// </summary>
        /// <param name="job">任务信息</param>
        /// <returns></returns>
        string CreateJob(Job job);

        /// <summary>
        /// 创建回调任务信息
        /// </summary>
        /// <param name="job">回调任务信息</param>
        /// <returns></returns>
        string CreateCallbackJob(CallbackJob job);

        /// <summary>
        /// 更新普通任务
        /// </summary>
        /// <param name="job">任务信息</param>
        void UpdateJob(Job job);

        /// <summary>
        /// 更新回调任务信息
        /// </summary>
        /// <param name="job">回调任务信息</param>
        void UpdateCallbackJob(CallbackJob job);

        /// <summary>
        /// 删除普通任务
        /// </summary>
        /// <param name="id">任务标识</param>
        void DeleteJob(string id);

        /// <summary>
        /// 删除回调任务
        /// </summary>
        /// <param name="id">任务标识</param>
        void DeleteCallbackJob(string id);

        /// <summary>
        /// 触发普通任务
        /// </summary>
        /// <param name="id">任务标识</param>
        void FireJob(string id);

        /// <summary>
        /// 触发回调任务
        /// </summary>
        /// <param name="id">任务标识</param>
        void FireCallbackJob(string id);
    }
}
