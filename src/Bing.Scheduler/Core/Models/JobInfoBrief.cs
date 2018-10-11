using System;

namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 任务信息报告
    /// </summary>
    public class JobInfoBrief
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 上次执行时间
        /// </summary>
        public DateTime? LastExecutedTime { get; set; }

        /// <summary>
        /// 下次执行时间
        /// </summary>
        public DateTime? NextExecutedTime { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public string ErrorMsg { get; set; }

        /// <summary>
        /// 任务状态
        /// </summary>
        public TriggerState TriggerState { get; set; }

        /// <summary>
        /// 任务状态显示文本
        /// </summary>
        public string TriggerStateText => TriggerState.Description();
    }
}
