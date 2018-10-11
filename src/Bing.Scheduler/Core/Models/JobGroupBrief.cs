using System.Collections.Generic;

namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 任务分组报告
    /// </summary>
    public class JobGroupBrief
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 任务信息列表
        /// </summary>
        public List<JobInfoBrief> JobInfos { get; set; } = new List<JobInfoBrief>();
    }
}
