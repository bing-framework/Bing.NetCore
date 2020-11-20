using System;
using System.Collections.Generic;

namespace Bing.Domain.Entities.Events
{
    /// <summary>
    /// 领域事件处理器类型存储器
    /// </summary>
    public interface IDomainEventHandlerTypeStore
    {
        /// <summary>
        /// 添加
        /// </summary>
        /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
        /// <typeparam name="TDomainEventHandler">领域事件处理器类型</typeparam>
        void Add<TDomainEvent, TDomainEventHandler>() 
            where TDomainEvent : DomainEvent
            where TDomainEventHandler : IDomainEventHandler<TDomainEvent>;

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="eventType">领域事件类型</param>
        /// <param name="handlerType">领域事件处理器类型</param>
        void Add(Type eventType, Type handlerType);

        /// <summary>
        /// 获取领域事件处理器列表
        /// </summary>
        /// <param name="eventType">事件类型</param>
        IReadOnlyCollection<DomainEventHandlerInfo> GetHandlers(Type eventType);
    }
}
