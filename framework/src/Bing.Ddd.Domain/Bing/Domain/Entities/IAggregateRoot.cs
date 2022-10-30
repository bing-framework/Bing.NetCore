using System.Collections.Generic;
using Bing.Domain.Entities.Events;

namespace Bing.Domain.Entities;

/// <summary>
/// 聚合根
/// </summary>
public interface IAggregateRoot : IEntity, IVersion
{
    /// <summary>
    /// 获取领域事件集合
    /// </summary>
    IReadOnlyCollection<DomainEvent> GetDomainEvents();

    /// <summary>
    /// 添加领域事件
    /// </summary>
    /// <param name="event">领域事件</param>
    void AddDomainEvent(DomainEvent @event);

    /// <summary>
    /// 移除领域事件
    /// </summary>
    /// <param name="event">领域事件</param>
    void RemoveDomainEvent(DomainEvent @event);

    /// <summary>
    /// 清空领域事件
    /// </summary>
    void ClearDomainEvents();
}

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IAggregateRoot<out TKey> : IEntity<TKey>, IAggregateRoot
{
}

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
public interface IAggregateRoot<in TEntity, out TKey> : IEntity<TEntity, TKey>, IAggregateRoot<TKey>
    where TEntity : IAggregateRoot
{
}