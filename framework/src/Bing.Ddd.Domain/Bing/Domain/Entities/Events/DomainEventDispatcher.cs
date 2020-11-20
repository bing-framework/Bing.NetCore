using System;
using System.Threading.Tasks;
using Bing.Helpers;

namespace Bing.Domain.Entities.Events
{
    /// <summary>
    /// 领域事件调度器
    /// </summary>
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        /// <summary>
        /// 领域事件处理器类型存储器
        /// </summary>
        private readonly IDomainEventHandlerTypeStore _eventHandlerTypeStore;

        /// <summary>
        /// 领域事件处理器工厂
        /// </summary>
        private readonly IDomainHandlerFactory _handlerFactory;

        /// <summary>
        /// 初始化一个<see cref="DomainEventDispatcher"/>类型的实例
        /// </summary>
        /// <param name="eventHandlerTypeStore">领域事件处理器类型存储器</param>
        /// <param name="handlerFactory">领域事件处理器工厂</param>
        public DomainEventDispatcher(IDomainEventHandlerTypeStore eventHandlerTypeStore, IDomainHandlerFactory handlerFactory)
        {
            _handlerFactory = handlerFactory;
            _eventHandlerTypeStore = eventHandlerTypeStore ?? new DomainEventHandlerTypeStore();
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
        /// <typeparam name="TDomainEventHandler">领域事件处理器类型</typeparam>
        public virtual bool Register<TDomainEvent, TDomainEventHandler>() 
            where TDomainEvent : DomainEvent 
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent>
        {
            var handlerType = typeof(TDomainEventHandler);
            return Register<TDomainEvent>(handlerType);
        }

        /// <summary>
        /// 注册
        /// </summary>
        /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
        /// <param name="handlerType">领域事件处理器类型</param>
        public virtual bool Register<TDomainEvent>(Type handlerType) 
            where TDomainEvent : DomainEvent => 
            Register(typeof(TDomainEvent), handlerType);

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="eventType">领域事件类型</param>
        /// <param name="handlerType">领域事件处理器类型</param>
        public virtual bool Register(Type eventType, Type handlerType)
        {
            Check.NotNull(eventType, nameof(eventType));
            Check.NotNull(handlerType, nameof(handlerType));

            if(!eventType.IsEvent())
                throw new ArgumentException($"领域事件 {eventType} 应该继承 {nameof(DomainEvent)} 类.");
            if (handlerType.CanHandle(eventType))
            {
                _eventHandlerTypeStore.Add(eventType,handlerType);
                return true;
            }
            throw new ArgumentException($"类型 {handlerType} 不是有效的领域事件处理器");
        }

        /// <summary>
        /// 调度
        /// </summary>
        /// <param name="event">领域事件</param>
        public virtual async Task DispatchAsync(DomainEvent @event)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            var eventType = @event.GetType();
            var handlerInfos = _eventHandlerTypeStore.GetHandlers(eventType);
            foreach (var handlerInfo in handlerInfos)
            {
                var handler = _handlerFactory.Create(handlerInfo.Type);
                if (handler == null)
                    throw new ApplicationException($"创建领域事件处理器 {handlerInfo.Type} 对象失败.");
                if (handlerInfo.Method.Invoke(handler, new object[] {@event}) is Task task)
                    await task;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}
