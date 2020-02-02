using System;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Utils.Extensions;

namespace Bing.Events
{
    /// <summary>
    /// 泛型事件名称 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class GenericEventNameAttribute : Attribute, IEventNameProvider
    {
        /// <summary>
        /// 前缀
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// 后缀
        /// </summary>
        public string Postfix { get; set; }

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public virtual string GetName(Type eventType)
        {
            if (!eventType.IsGenericType)
                throw new Warning($"给定类型不是泛型: {eventType.AssemblyQualifiedName}");
            var genericArguments = eventType.GetGenericArguments();
            if (genericArguments.Length > 1)
                throw new Warning($"给定类型具有多个泛型参数: {eventType.AssemblyQualifiedName}");
            var eventName = EventNameAttribute.GetNameOrDefault(genericArguments[0]);
            if (!Prefix.IsEmpty())
                eventName = $"{Prefix}{eventName}";
            if (!Postfix.IsEmpty())
                eventName = $"{eventName}{Postfix}";
            return eventName;
        }
    }
}
