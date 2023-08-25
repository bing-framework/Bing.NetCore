using Bing.Domain.Entities.Events;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 基本聚合根
/// </summary>
public abstract class BasicAggregateRoot<TEntity> : BasicAggregateRoot<TEntity, Guid>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化一个<see cref="BasicAggregateRoot{TEntity}"/>类型的实例
    /// </summary>
    protected BasicAggregateRoot() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BasicAggregateRoot{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected BasicAggregateRoot(Guid id) : base(id)
    {
    }
}

/// <summary>
/// 基本聚合根
/// </summary>
public abstract class BasicAggregateRoot<TEntity, TKey> : EntityBase<TEntity, TKey>, IAggregateRoot<TEntity, TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 领域事件列表
    /// </summary>
    private List<DomainEvent> _domainEvents;

    /// <summary>
    /// 初始化一个<see cref="BasicAggregateRoot{TEntity,TKey}"/>类型的实例
    /// </summary>
    protected BasicAggregateRoot() : this(default)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="BasicAggregateRoot{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected BasicAggregateRoot(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 获取领域事件集合
    /// </summary>
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents?.AsReadOnly();

    /// <summary>
    /// 添加领域事件
    /// </summary>
    /// <param name="event">领域事件</param>
    public void AddDomainEvent(DomainEvent @event)
    {
        _domainEvents ??= new List<DomainEvent>();
        _domainEvents.Add(@event);
    }

    /// <summary>
    /// 移除领域事件
    /// </summary>
    /// <param name="event">领域事件</param>
    public void RemoveDomainEvent(DomainEvent @event) => _domainEvents?.Remove(@event);

    /// <summary>
    /// 清空领域事件
    /// </summary>
    public void ClearDomainEvents() => _domainEvents?.Clear();
}
