using System;
using Quartz;

namespace Bing.Quartz.Abstractions
{
    /// <summary>
    /// 任务上下文
    /// </summary>
    public interface IJobTaskContext
    {
        /// <summary>
        /// 任务标识
        /// </summary>
        Guid JobId { get; set; }

        /// <summary>
        /// Quartz任务执行上下文
        /// </summary>
        IJobExecutionContext JobExecutionContext { get; set; }
    }
}
