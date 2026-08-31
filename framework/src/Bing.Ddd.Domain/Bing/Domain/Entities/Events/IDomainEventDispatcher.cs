namespace Bing.Domain.Entities.Events;

/// <summary>
/// 提供领域事件处理器注册和事件分发能力。
/// </summary>
public interface IDomainEventDispatcher : IDisposable
{
    /// <summary>
    /// 注册指定领域事件类型对应的处理器类型。
    /// </summary>
    /// <typeparam name="TDomainEvent">要处理的领域事件类型。</typeparam>
    /// <typeparam name="TDomainEventHandler">实现该事件处理契约的处理器类型。</typeparam>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    bool Register<TDomainEvent, TDomainEventHandler>() 
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent>;

    /// <summary>
    /// 注册指定领域事件类型对应的处理器类型。
    /// </summary>
    /// <typeparam name="TDomainEvent">要处理的领域事件类型。</typeparam>
    /// <param name="handlerType">领域事件处理器类型。</param>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    bool Register<TDomainEvent>(Type handlerType) where TDomainEvent : DomainEvent;

    /// <summary>
    /// 注册指定领域事件类型和处理器类型的映射。
    /// </summary>
    /// <param name="eventType">领域事件类型。</param>
    /// <param name="handlerType">能够处理该事件的处理器类型。</param>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    bool Register(Type eventType, Type handlerType);

    /// <summary>
    /// 异步分发领域事件，并依次调用已注册的处理器。
    /// </summary>
    /// <param name="event">要分发的领域事件，不能为 <c>null</c>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> 为 <c>null</c> 时抛出。</exception>
    Task DispatchAsync(DomainEvent @event);
}