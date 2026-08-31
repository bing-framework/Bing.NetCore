using Bing.Helpers;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 使用已注册处理器分发领域事件。
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// 保存领域事件与处理器类型的注册关系。
    /// </summary>
    private readonly IDomainEventHandlerTypeStore _eventHandlerTypeStore;

    /// <summary>
    /// 创建已注册的领域事件处理器实例。
    /// </summary>
    private readonly IDomainHandlerFactory _handlerFactory;

    /// <summary>
    /// 初始化 <see cref="DomainEventDispatcher"/> 的实例。
    /// </summary>
    /// <param name="eventHandlerTypeStore">领域事件处理器类型存储器；为 <c>null</c> 时使用默认存储器。</param>
    /// <param name="handlerFactory">领域事件处理器工厂。</param>
    public DomainEventDispatcher(IDomainEventHandlerTypeStore eventHandlerTypeStore, IDomainHandlerFactory handlerFactory)
    {
        _handlerFactory = handlerFactory;
        _eventHandlerTypeStore = eventHandlerTypeStore ?? new DomainEventHandlerTypeStore();
    }

    /// <summary>
    /// 注册指定领域事件类型对应的处理器类型。
    /// </summary>
    /// <typeparam name="TDomainEvent">领域事件类型。</typeparam>
    /// <typeparam name="TDomainEventHandler">领域事件处理器类型。</typeparam>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    public virtual bool Register<TDomainEvent, TDomainEventHandler>() 
        where TDomainEvent : DomainEvent 
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent>
    {
        var handlerType = typeof(TDomainEventHandler);
        return Register<TDomainEvent>(handlerType);
    }

    /// <summary>
    /// 注册指定领域事件类型对应的处理器类型。
    /// </summary>
    /// <typeparam name="TDomainEvent">领域事件类型。</typeparam>
    /// <param name="handlerType">领域事件处理器类型。</param>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    public virtual bool Register<TDomainEvent>(Type handlerType) 
        where TDomainEvent : DomainEvent => 
        Register(typeof(TDomainEvent), handlerType);

    /// <summary>
    /// 注册指定领域事件类型对应的处理器类型。
    /// </summary>
    /// <param name="eventType">领域事件类型。</param>
    /// <param name="handlerType">领域事件处理器类型。</param>
    /// <returns>注册成功时返回 <c>true</c>。</returns>
    /// <exception cref="ArgumentNullException">任一类型参数为 <c>null</c> 时抛出。</exception>
    /// <exception cref="ArgumentException">事件类型不是领域事件，或处理器不能处理该事件时抛出。</exception>
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
    /// 异步分发领域事件，并按注册顺序调用处理器。
    /// </summary>
    /// <param name="event">要分发的领域事件。</param>
    /// <exception cref="ArgumentNullException"><paramref name="event"/> 为 <c>null</c> 时抛出。</exception>
    /// <exception cref="ApplicationException">处理器实例创建失败时抛出。</exception>
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
    /// 释放调度器占用的资源。
    /// </summary>
    /// <remarks>当前调度器不持有需要释放的资源。</remarks>
    public virtual void Dispose()
    {
    }
}