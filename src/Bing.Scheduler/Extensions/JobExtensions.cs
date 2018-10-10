using System;
using Bing.Scheduler.Abstractions;
using Bing.Scheduler.Core;

namespace Bing.Scheduler.Extensions
{
    /// <summary>
    /// 任务(<see cref="IJob"/>) 扩展
    /// </summary>
    internal static class JobExtensions
    {
        /// <summary>
        /// 转换成任务上下文
        /// </summary>
        /// <param name="job">任务</param>
        /// <returns></returns>
        public static JobContext ToContext(this IJob job)
        {
            return new JobContext()
            {
                Id = job.Id,
                Name = job.Name,
                Content = job.Content,
                Cron = job.Cron,
                FireTime = DateTime.Now,
                Group = job.Group
            };
        }
    }
}
