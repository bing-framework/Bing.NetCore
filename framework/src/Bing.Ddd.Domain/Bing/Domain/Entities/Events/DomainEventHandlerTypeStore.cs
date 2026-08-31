using System.Collections.Concurrent;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 保存领域事件与其处理器方法信息的内存存储器。
/// </summary>
public class DomainEventHandlerTypeStore : IDomainEventHandlerTypeStore
{
    /// <summary>
    /// 按领域事件类型索引处理器信息集合。
    /// </summary>
    private readonly ConcurrentDictionary<Type, HashSet<DomainEventHandlerInfo>> _eventHandlerTypesDict;

    /// <summary>
    /// 未找到处理器时返回的共享空集合。
    /// </summary>
    private static readonly IReadOnlyCollection<DomainEventHandlerInfo> Empty = new List<DomainEventHandlerInfo>();

    /// <summary>
    /// 初始化空的领域事件处理器类型存储器。
    /// </summary>
    public DomainEventHandlerTypeStore() => _eventHandlerTypesDict = new ConcurrentDictionary<Type, HashSet<DomainEventHandlerInfo>>();

    /// <summary>
    /// 添加指定领域事件和处理器类型的映射。
    /// </summary>
    /// <typeparam name="TDomainEvent">领域事件类型。</typeparam>
    /// <typeparam name="TDomainEventHandler">领域事件处理器类型。</typeparam>
    public void Add<TDomainEvent, TDomainEventHandler>()
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent> =>
        Add(typeof(TDomainEvent), typeof(TDomainEventHandler));

    /// <summary>
    /// 添加指定领域事件和处理器类型的映射。
    /// </summary>
    /// <param name="eventType">领域事件类型。</param>
    /// <param name="handlerType">领域事件处理器类型。</param>
    /// <exception cref="BingFrameworkException">处理器类型没有 <c>HandleAsync</c> 方法时抛出。</exception>
    public void Add(Type eventType, Type handlerType)
    {
        var methodInfo = handlerType.GetMethod("HandleAsync");
        if (methodInfo == null)
            throw new BingFrameworkException($"类型 {handlerType.FullName} 中找不到处理方法 HandleAsync");
        if (!_eventHandlerTypesDict.ContainsKey(eventType))
            _eventHandlerTypesDict.TryAdd(eventType, new HashSet<DomainEventHandlerInfo>());
        _eventHandlerTypesDict[eventType].Add(new DomainEventHandlerInfo(handlerType, methodInfo));
    }

    /// <summary>
    /// 获取指定领域事件对应的处理器信息集合。
    /// </summary>
    /// <param name="eventType">要查询的领域事件类型。</param>
    /// <returns>已注册的处理器信息集合；没有注册处理器时返回共享空集合。</returns>
    public IReadOnlyCollection<DomainEventHandlerInfo> GetHandlers(Type eventType)
    {
        if (_eventHandlerTypesDict.TryGetValue(eventType, out var handlerTypes))
            return handlerTypes;
        return Empty;
    }
}