using Bing.Domain.Entities.Events;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 为使用 <see cref="Guid"/> 标识的聚合根提供基础实现。
/// </summary>
/// <typeparam name="TEntity">聚合根的具体实体类型。</typeparam>
public abstract class BasicAggregateRoot<TEntity> : BasicAggregateRoot<TEntity, Guid>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化 <see cref="BasicAggregateRoot{TEntity}"/> 的实例，并使用空标识。
    /// </summary>
    protected BasicAggregateRoot() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="BasicAggregateRoot{TEntity}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected BasicAggregateRoot(Guid id) : base(id)
    {
    }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供领域事件管理能力。
/// </summary>
/// <typeparam name="TEntity">聚合根的具体实体类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
public abstract class BasicAggregateRoot<TEntity, TKey> : EntityBase<TEntity, TKey>, IAggregateRoot<TEntity, TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 延迟创建的领域事件列表，按添加顺序保存待分发事件。
    /// </summary>
    private List<DomainEvent> _domainEvents;

    /// <summary>
    /// 初始化 <see cref="BasicAggregateRoot{TEntity,TKey}"/> 的实例，并使用默认标识。
    /// </summary>
    protected BasicAggregateRoot() : this(default)
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="BasicAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected BasicAggregateRoot(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 获取当前聚合根记录的领域事件。
    /// </summary>
    /// <returns>领域事件的只读集合；尚未添加事件时返回 <c>null</c>。</returns>
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents?.AsReadOnly();

    /// <summary>
    /// 添加待分发的领域事件。
    /// </summary>
    /// <param name="event">要记录的领域事件。</param>
    public void AddDomainEvent(DomainEvent @event)
    {
        _domainEvents ??= new List<DomainEvent>();
        _domainEvents.Add(@event);
    }

    /// <summary>
    /// 移除指定的领域事件。
    /// </summary>
    /// <param name="event">要移除的领域事件。</param>
    /// <remarks>未初始化事件列表或事件不存在时不执行任何操作。</remarks>
    public void RemoveDomainEvent(DomainEvent @event) => _domainEvents?.Remove(@event);

    /// <summary>
    /// 清空当前已记录的领域事件。
    /// </summary>
    /// <remarks>未初始化事件列表时不执行任何操作。</remarks>
    public void ClearDomainEvents() => _domainEvents?.Clear();
}
