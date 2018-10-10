namespace Bing.Scheduler.Abstractions
{
    /// <summary>
    /// 任务
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// 任务标识
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// 任务分组
        /// </summary>
        string Group { get; set; }

        /// <summary>
        /// 任务名称：完整的类名
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 定时表达式
        /// </summary>
        string Cron { get; set; }

        /// <summary>
        /// 任务描述
        /// </summary>
        string Content { get; set; }
    }
}
