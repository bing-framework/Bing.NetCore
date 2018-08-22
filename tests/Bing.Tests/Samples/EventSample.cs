using System.Collections.Generic;
using System.Linq;
using Bing.Events;
using Bing.Events.Handlers;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 事件样例
    /// </summary>
    public class EventSample:Event
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 事件处理器服务样例
    /// </summary>
    public class EventHandlerManagerSample : IEventHandlerManager
    {
        /// <summary>
        /// 事件处理器列表
        /// </summary>
        private readonly IEventHandler[] _handlers;

        /// <summary>
        /// 初始化一个<see cref="EventHandlerManagerSample"/>类型的实例
        /// </summary>
        /// <param name="handlers">事件处理器列表</param>
        public EventHandlerManagerSample(params IEventHandler[] handlers)
        {
            _handlers = handlers;
        }

        /// <summary>
        /// 获取事件处理器列表
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <returns></returns>
        public List<IEventHandler<TEvent>> GetHandlers<TEvent>() where TEvent : IEvent
        {
            return _handlers.Select(t => t as IEventHandler<TEvent>).ToList();
        }
    }
}
