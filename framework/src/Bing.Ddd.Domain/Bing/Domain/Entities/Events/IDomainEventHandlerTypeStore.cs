namespace Bing.Domain.Entities.Events;

/// <summary>
/// 定义领域事件与其处理器类型映射的存储契约。
/// </summary>
public interface IDomainEventHandlerTypeStore
{
    /// <summary>
    /// 添加指定领域事件和处理器类型的映射。
    /// </summary>
    /// <typeparam name="TDomainEvent">领域事件类型。</typeparam>
    /// <typeparam name="TDomainEventHandler">领域事件处理器类型。</typeparam>
    void Add<TDomainEvent, TDomainEventHandler>() 
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent>;

    /// <summary>
    /// 添加指定领域事件和处理器类型的映射。
    /// </summary>
    /// <param name="eventType">领域事件类型。</param>
    /// <param name="handlerType">领域事件处理器类型。</param>
    void Add(Type eventType, Type handlerType);

    /// <summary>
    /// 获取指定领域事件对应的处理器信息集合。
    /// </summary>
    /// <param name="eventType">要查询的领域事件类型。</param>
    /// <returns>已注册的处理器信息集合；没有注册处理器时返回空集合。</returns>
    IReadOnlyCollection<DomainEventHandlerInfo> GetHandlers(Type eventType);
}