using System.Threading.Tasks;
using Bing.Events.Handlers;
using Bing.Events.Messages;

namespace Bing.Events.Default
{
    /// <summary>
    /// 事件总线
    /// </summary>
    public class EventBus : IEventBus
    {
        /// <summary>
        /// 事件处理器服务
        /// </summary>
        public IEventHandlerManager Manager { get; set; }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        public IMessageEventBus MessageEventBus { get; set; }

        /// <summary>
        /// 初始化一个<see cref="EventBus"/>类型的实例
        /// </summary>
        /// <param name="manager">事件处理器服务</param>
        /// <param name="messageEventBus">消息事件总线</param>
        public EventBus(IEventHandlerManager manager, IMessageEventBus messageEventBus = null)
        {
            Manager = manager;
            MessageEventBus = messageEventBus;
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
            await PublishMessageEventsAsync(@event);
        }

        /// <summary>
        /// 发布消息事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="event">事件</param>
        /// <returns></returns>
        private async Task PublishMessageEventsAsync<TEvent>(TEvent @event)
        {
            if (MessageEventBus == null)
            {
                return;
            }

            if (@event is IMessageEvent messageEvent)
            {
                await MessageEventBus.PublishAsync(messageEvent);
            }
        }
    }
}
