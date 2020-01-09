using System;
using System.Threading.Tasks;
using Bing.Quartz.Abstractions;
using Quartz;

namespace Bing.Quartz.Core
{
    /// <summary>
    /// 任务基类
    /// </summary>
    public abstract class JobTaskBase : IJobTask
    {
        /// <summary>
        /// 工作任务日志记录器
        /// </summary>
        protected readonly IJobLogger Logger;

        /// <summary>
        /// 初始化一个<see cref="JobTaskBase"/>类型的实例
        /// </summary>
        /// <param name="logger">工作任务日志记录器</param>
        protected JobTaskBase(IJobLogger logger) => Logger = logger;

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context">任务执行上下文</param>
        public async Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.JobDataMap["id"];
            Logger.JobId = jobId == null ? Guid.Empty : Guid.Parse(jobId.ToString());
            await Logger.Info("任务开始");
            try
            {
                await Execute(new JobTaskContext() { JobId = Logger.JobId, JobExecutionContext = context });
            }
            catch (Exception ex)
            {
                await Logger.Error("任务异常: " + ex);
            }

            await Logger.Info("任务结束");
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="context">任务上下文</param>
        public abstract Task Execute(IJobTaskContext context);
    }
}
