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
    /// 初始化一个 <see cref="DomainEventHandlerTypeStore"/> 类型的实例。
    /// </summary>
    public DomainEventHandlerTypeStore() => _eventHandlerTypesDict = new ConcurrentDictionary<Type, HashSet<DomainEventHandlerInfo>>();

    /// <inheritdoc />
    public void Add<TDomainEvent, TDomainEventHandler>()
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent> =>
        Add(typeof(TDomainEvent), typeof(TDomainEventHandler));

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IReadOnlyCollection<DomainEventHandlerInfo> GetHandlers(Type eventType)
    {
        if (_eventHandlerTypesDict.TryGetValue(eventType, out var handlerTypes))
            return handlerTypes;
        return Empty;
    }
}