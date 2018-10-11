namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 触发器类型
    /// </summary>
    public enum TriggerType
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// Cron表达式
        /// </summary>
        Cron = 1,
        /// <summary>
        /// 简单表达式
        /// </summary>
        Simple = 2
    }
}
