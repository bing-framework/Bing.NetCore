using Bing.Events.Messages;

namespace Bing.Samples.Domain.Events
{
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public class LogMessageEvent:MessageEvent
    {
        public LogMessageEvent(LogMessage data)
        {
            Name = "WriteLog";
            Data = data;
            Send = true;
        }
    }

    /// <summary>
    /// 日志消息
    /// </summary>
    public class LogMessage
    {
        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 转换为事件
        /// </summary>
        public LogMessageEvent ToEvent()=>new LogMessageEvent(this);
    }
}
