using System;
using System.Text;
using Bing.Extensions;
using Bing.Utils.Json;

namespace Bing.Events
{
    /// <summary>
    /// 事件
    /// </summary>
    public class Event : IEvent
    {
        /// <summary>
        /// 事件名称
        /// </summary>
        private readonly string _eventName;

        /// <summary>
        /// 初始化一个<see cref="Event"/>类型的实例
        /// </summary>
        public Event(string eventName = default)
        {
            Id = Helpers.Id.Guid();
            Time = DateTime.Now;
            _eventName = eventName;
        }

        /// <summary>
        /// 事件标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 事件时间
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        /// 获取事件名称
        /// </summary>
        public virtual string GetEventName()
        {
            var eventName = _eventName;
            if (eventName.IsEmpty())
            {
                var eventType = base.GetType();
                if (!eventType.IsGenericType)
                    eventName = EventNameAttribute.GetNameOrDefault(eventType);
                else
                {
                    var eventNameAttribute = GetType().GetAttribute<GenericEventNameAttribute>();
                    eventName = eventNameAttribute.GetName(eventType);
                }
            }
            return eventName;
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        public override string ToString()
        {
            var result = new StringBuilder();
            result.AppendLine($"事件标识: {Id}");
            result.AppendLine($"事件时间: {Time:yyyy-MM-dd HH:mm:ss.fff}");
            result.AppendLine($"事件数据: {(this).ToJson()}");
            return result.ToString();
        }
    }
}
