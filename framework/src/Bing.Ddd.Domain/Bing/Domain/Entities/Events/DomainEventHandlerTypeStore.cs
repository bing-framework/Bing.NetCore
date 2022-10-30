using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 领域事件处理器类型存储器
/// </summary>
public class DomainEventHandlerTypeStore : IDomainEventHandlerTypeStore
{
    /// <summary>
    /// 领域事件处理器类型字典
    /// </summary>
    private readonly ConcurrentDictionary<Type, HashSet<DomainEventHandlerInfo>> _eventHandlerTypesDict;

    /// <summary>
    /// 领域事件处理器信息空集合
    /// </summary>
    private static readonly IReadOnlyCollection<DomainEventHandlerInfo> Empty = new List<DomainEventHandlerInfo>();

    /// <summary>
    /// 初始化一个<see cref="DomainEventHandlerTypeStore"/>类型的实例
    /// </summary>
    public DomainEventHandlerTypeStore() => _eventHandlerTypesDict = new ConcurrentDictionary<Type, HashSet<DomainEventHandlerInfo>>();

    /// <summary>
    /// 添加
    /// </summary>
    /// <typeparam name="TDomainEvent">领域事件类型</typeparam>
    /// <typeparam name="TDomainEventHandler">领域事件处理器类型</typeparam>
    public void Add<TDomainEvent, TDomainEventHandler>()
        where TDomainEvent : DomainEvent
        where TDomainEventHandler : IDomainEventHandler<TDomainEvent> =>
        Add(typeof(TDomainEvent), typeof(TDomainEventHandler));

    /// <summary>
    /// 添加
    /// </summary>
    /// <param name="eventType">领域事件类型</param>
    /// <param name="handlerType">领域事件处理器类型</param>
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
    /// 获取领域事件处理器列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public IReadOnlyCollection<DomainEventHandlerInfo> GetHandlers(Type eventType)
    {
        if (_eventHandlerTypesDict.TryGetValue(eventType, out var handlerTypes))
            return handlerTypes;
        return Empty;
    }
}