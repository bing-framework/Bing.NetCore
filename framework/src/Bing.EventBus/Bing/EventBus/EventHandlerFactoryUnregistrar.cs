using System;

namespace Bing.EventBus
{
    /// <summary>
    /// 事件处理器工厂注销器
    /// </summary>
    public class EventHandlerFactoryUnregistrar : IDisposable
    {
        /// <summary>
        /// 事件总线
        /// </summary>
        private readonly IEventBus _eventBus;

        /// <summary>
        /// 事件类型
        /// </summary>
        private readonly Type _eventType;

        /// <summary>
        /// 事件处理器工厂
        /// </summary>
        private readonly IEventHandlerFactory _factory;

        /// <summary>
        /// 初始化一个<see cref="EventHandlerFactoryUnregistrar"/>类型的实例
        /// </summary>
        /// <param name="eventBus">事件总线</param>
        /// <param name="eventType">事件类型</param>
        /// <param name="factory">事件处理器工厂</param>
        public EventHandlerFactoryUnregistrar(IEventBus eventBus, Type eventType, IEventHandlerFactory factory)
        {
            _eventBus = eventBus;
            _eventType = eventType;
            _factory = factory;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _eventBus.Unsubscribe(_eventType, _factory);
        }
    }
}
