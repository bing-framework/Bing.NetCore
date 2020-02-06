using System;
using System.Threading.Tasks;
using Bing.Events.Handlers;

namespace Bing.Events.Default
{
    /// <summary>
    /// 事件总线
    /// </summary>
    public class EventBus : ISimpleEventBus
    {
        /// <summary>
        /// 事件处理器服务
        /// </summary>
        public IEventHandlerManager Manager { get; set; }

        /// <summary>
        /// 初始化一个<see cref="EventBus"/>类型的实例
        /// </summary>
        /// <param name="manager">事件处理器服务</param>
        public EventBus(IEventHandlerManager manager)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="event">事件</param>
        /// <returns></returns>
        public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
        {
            var handlers = Manager.GetHandlers<TEvent>();
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers)
            {
                if (handler == null)
                {
                    continue;
                }
                await handler.HandleAsync(@event);
            }
        }
    }
}
