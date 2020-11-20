using System;
using System.Threading.Tasks;

namespace Bing.Domain.Entities.Events
{
    /// <summary>
    /// 领域事件调度器
    /// </summary>
    public interface IDomainEventDispatcher : IDisposable
    {
        /// <summary>
        /// 注册
        /// </summary>
        /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
        /// <typeparam name="TDomainEventHandler">领域事件处理器类型</typeparam>
        bool Register<TDomainEvent, TDomainEventHandler>() 
            where TDomainEvent : DomainEvent
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent>;

        /// <summary>
        /// 注册
        /// </summary>
        /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
        /// <param name="handlerType">领域事件处理器类型</param>
        bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent;

        /// <summary>
        /// 注册
        /// </summary>
        /// <param name="eventType">领域事件类型</param>
        /// <param name="handlerType">领域事件处理器类型</param>
        bool Register(Type eventType, Type handlerType);

        /// <summary>
        /// 调度
        /// </summary>
        /// <param name="event">领域事件</param>
        Task DispatchAsync(DomainEvent @event);
    }
}
