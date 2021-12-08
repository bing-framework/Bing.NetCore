using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bing.EventBus.Local
{
    /// <summary>
    /// 基于内存的本地事件总线
    /// </summary>
    public class LocalEventBus : ILocalEventBus
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化一个<see cref="LocalEventBus"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public LocalEventBus(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent
        {
            if (@event == null)
                return;
            var eventType = @event.GetType();
            var handlers = GetEventHandles(eventType);
            if (handlers == null)
                return;
            foreach (var handler in handlers.Where(t => t.Enabled).OrderBy(t => t.Order))
            {
                var method = typeof(ILocalEventHandler<>).MakeGenericType(eventType).GetMethod("HandleAsync", new[] { eventType });
                if (method == null)
                    return;
                var result = method.Invoke(handler, new object[] { @event });
                if (result == null)
                    return;
                await (Task)result;
            }
        }

        /// <summary>
        /// 获取本地事件处理器列表
        /// </summary>
        /// <param name="eventType">事件类型</param>
        private IEnumerable<ILocalEventHandler> GetEventHandles(Type eventType)
        {
            var handlerType = typeof(ILocalEventHandler<>).MakeGenericType(eventType);
            var serviceType = typeof(IEnumerable<>).MakeGenericType(handlerType);
            var handlers = _serviceProvider.GetService(serviceType);
            return handlers as IEnumerable<ILocalEventHandler>;
        }
    }
}
