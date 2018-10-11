namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 任务键
    /// </summary>
    public class JobKey
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 任务分组
        /// </summary>
        public string Group { get; set; }
    }
}
