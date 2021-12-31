using System;

namespace Bing.EventBus
{
    /// <summary>
    /// 事件处理释放包装器
    /// </summary>
    public class EventHandlerDisposeWrapper : IEventHandlerDisposeWrapper
    {
        /// <summary>
        /// 释放操作
        /// </summary>
        private readonly Action _disposeAction;

        /// <summary>
        /// 事件处理器
        /// </summary>
        public IEventHandler EventHandler { get; }

        /// <summary>
        /// 初始化一个<see cref="EventHandlerDisposeWrapper"/>类型的实例
        /// </summary>
        /// <param name="eventHandler">事件处理器</param>
        /// <param name="disposeAction">释放操作</param>
        public EventHandlerDisposeWrapper(IEventHandler eventHandler, Action disposeAction = null)
        {
            EventHandler = eventHandler;
            _disposeAction = disposeAction;
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose() => _disposeAction?.Invoke();
    }
}
