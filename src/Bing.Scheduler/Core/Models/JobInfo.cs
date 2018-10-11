using System;

namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 任务信息
    /// </summary>
    public class JobInfo
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
        /// 开始时间
        /// </summary>
        public DateTime BeginTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }

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

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 时间间隔
        /// </summary>
        public string Interval { get; set; }

        /// <summary>
        /// 请求API的地址
        /// </summary>
        public string RequestUrl { get; set; }
    }
}
