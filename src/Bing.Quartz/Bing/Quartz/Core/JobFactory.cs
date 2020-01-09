using System;
using Quartz;
using Quartz.Spi;

namespace Bing.Quartz.Core
{
    /// <summary>
    /// 任务工厂
    /// </summary>
    public class JobFactory : IJobFactory
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化一个<see cref="JobFactory"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public JobFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        /// <summary>
        /// 创建新任务
        /// </summary>
        /// <param name="bundle">触发器</param>
        /// <param name="scheduler">调度器</param>
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler) => _serviceProvider.GetService(bundle.JobDetail.JobType) as IJob;

        /// <summary>
        /// 返回任务
        /// </summary>
        /// <param name="job">任务</param>
        public void ReturnJob(IJob job) => (job as IDisposable)?.Dispose();
    }
}
