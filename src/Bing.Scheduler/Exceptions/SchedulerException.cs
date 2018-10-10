using System;

namespace Bing.Scheduler.Exceptions
{
    /// <summary>
    /// 任务调度异常
    /// </summary>
    public class SchedulerException:Exception
    {
        /// <summary>
        /// 初始化一个<see cref="SchedulerException"/>类型的实例
        /// </summary>
        public SchedulerException() { }

        /// <summary>
        /// 初始化一个<see cref="SchedulerException"/>类型的实例
        /// </summary>
        /// <param name="msg">错误消息</param>
        /// <param name="inner">内部异常</param>
        public SchedulerException(string msg,Exception inner = null) : base(msg, inner) { }
    }
}
