using System;

namespace Bing.Scheduler.Core.Models
{
    /// <summary>
    /// 调度信息
    /// </summary>
    public class ScheduleInfo
    {
        /// <summary>
        /// 任务名称
        /// </summary>
        public string JobName { get; set; }

        /// <summary>
        /// 任务标题
        /// </summary>
        public string JobTitle { get; set; }

        /// <summary>
        /// 任务分组
        /// </summary>
        public string JobGroup { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTimeOffset BeginTime { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTimeOffset? EndTime { get; set; }

        /// <summary>
        /// Cron表达式
        /// </summary>
        public string Cron { get; set; }

        /// <summary>
        /// 执行次数。默认：无限循环
        /// </summary>
        public int? RunTimes { get; set; }

        /// <summary>
        /// 执行时间间隔。单位：秒（如果有Cron，则IntervalSecond失效）
        /// </summary>
        public int? IntervalSecond { get; set; }

        /// <summary>
        /// 触发器类型
        /// </summary>
        public TriggerType TriggerType { get; set; }

        /// <summary>
        /// 请求Url
        /// </summary>
        public string RequestUrl { get; set; }

        /// <summary>
        /// 请求参数（Post、Put请求用）
        /// </summary>
        public string RequestParameters { get; set; }

        /// <summary>
        /// 请求类型
        /// </summary>
        public RequestType RequestType { get; set; } = RequestType.Post;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }
    }
}
