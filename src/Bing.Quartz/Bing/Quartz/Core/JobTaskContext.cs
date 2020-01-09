using System;
using Bing.Quartz.Abstractions;
using Quartz;

namespace Bing.Quartz.Core
{
    /// <summary>
    /// 任务上下文
    /// </summary>
    public class JobTaskContext : IJobTaskContext
    {
        /// <summary>
        /// 任务标识
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// Quartz任务执行上下文
        /// </summary>
        public IJobExecutionContext JobExecutionContext { get; set; }
    }
}
