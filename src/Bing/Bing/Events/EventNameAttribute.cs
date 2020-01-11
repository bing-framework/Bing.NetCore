using System;
using System.Linq;
using Bing.Utils.Helpers;

namespace Bing.Events
{
    /// <summary>
    /// 事件名称 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EventNameAttribute : Attribute, IEventNameProvider
    {
        /// <summary>
        /// 名称
        /// </summary>
        public virtual string Name { get; }

        /// <summary>
        /// 初始化一个<see cref="EventNameAttribute"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        public EventNameAttribute(string name)
        {
            Check.NotNullOrEmpty(name, nameof(name));
            Name = name;
        }

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public string GetName(Type eventType) => Name;

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        public static string GetNameOrDefault<TEvent>() => GetNameOrDefault(typeof(TEvent));

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public static string GetNameOrDefault(Type eventType)
        {
            Check.NotNull(eventType, nameof(eventType));
            return eventType.GetCustomAttributes(true)
                       .OfType<IEventNameProvider>()
                       .FirstOrDefault()
                       ?.GetName(eventType)
                   ?? eventType.FullName;
        }
    }
}
