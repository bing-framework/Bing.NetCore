using System.ComponentModel;

namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 触发状态
    /// </summary>
    public enum TriggerState
    {
        /// <summary>
        /// 正常
        /// </summary>
        [Description("正常")]
        Normal = 0,
        /// <summary>
        /// 暂停
        /// </summary>
        [Description("暂停")]
        Paused = 1,
        /// <summary>
        /// 完成
        /// </summary>
        [Description("完成")]
        Complete = 2,
        /// <summary>
        /// 异常
        /// </summary>
        [Description("异常")]
        Error = 3,
        /// <summary>
        /// 阻塞
        /// </summary>
        [Description("阻塞")]
        Blocked = 4,
        /// <summary>
        /// 不存在
        /// </summary>
        [Description("不存在")]
        None = 5,
    }

    /// <summary>
    /// 触发状态扩展
    /// </summary>
    public static class TriggerStateExtensions
    {
        /// <summary>
        /// 获取描述
        /// </summary>
        /// <param name="state">触发状态</param>
        /// <returns></returns>
        public static string Description(this TriggerState state)
        {
            string result;
            switch (state)
            {
                case TriggerState.Normal:
                    result = "正常";
                    break;
                case TriggerState.Paused:
                    result = "暂停";
                    break;
                case TriggerState.Complete:
                    result = "完成";
                    break;
                case TriggerState.Error:
                    result = "异常";
                    break;
                case TriggerState.Blocked:
                    result = "阻塞";
                    break;
                case TriggerState.None:
                    result = "不存在";
                    break;
                default:
                    result = "未知";
                    break;
            }

            return result;
        }
    }
}
