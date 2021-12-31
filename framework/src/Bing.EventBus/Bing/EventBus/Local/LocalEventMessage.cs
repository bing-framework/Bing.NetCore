using System;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 本地事件消息
    /// </summary>
    public class LocalEventMessage
    {
        /// <summary>
        /// 事件标识
        /// </summary>
        public string EventId { get; }

        /// <summary>
        /// 事件数据
        /// </summary>
        public object EventData { get; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// 初始化一个<see cref="LocalEventMessage"/>类型的实例
        /// </summary>
        /// <param name="eventId">事件标识</param>
        /// <param name="eventData">事件数据</param>
        /// <param name="eventType">事件类型</param>
        public LocalEventMessage(string eventId, object eventData, Type eventType)
        {
            EventId = eventId;
            EventData = eventData;
            EventType = eventType;
        }
    }
}
