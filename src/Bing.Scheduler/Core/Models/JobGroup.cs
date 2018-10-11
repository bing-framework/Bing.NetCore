using System.Collections.Generic;

namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 任务分组
    /// </summary>
    public class JobGroup
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 任务信息列表
        /// </summary>
        public List<JobInfo> JobInfos { get; set; } = new List<JobInfo>();
    }
}
