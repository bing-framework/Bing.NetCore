using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Events
{
    /// <summary>
    /// 事件名称 特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EventNameAttribute : Attribute, IEventNameProvider
    {

        /// <summary>
        /// 获取名称
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public string GetName(Type eventType)
        {
            throw new NotImplementedException();
        }
    }
}
