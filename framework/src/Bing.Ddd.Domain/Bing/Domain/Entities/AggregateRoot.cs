using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 为使用 <see cref="Guid"/> 标识且需要乐观并发控制的聚合根提供基类。
/// </summary>
/// <typeparam name="TEntity">聚合根的具体实体类型。</typeparam>
public abstract class AggregateRoot<TEntity> : BasicAggregateRoot<TEntity>, IVersion
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化 <see cref="AggregateRoot{TEntity}"/> 的实例，并使用空标识。
    /// </summary>
    protected AggregateRoot() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="AggregateRoot{TEntity}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <inheritdoc />
    [DisableAuditing]
    public virtual byte[] Version { get; set; }
}

/// <summary>
/// 为具有指定标识类型且需要乐观并发控制的聚合根提供基类。
/// </summary>
/// <typeparam name="TEntity">聚合根的具体实体类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
public abstract class AggregateRoot<TEntity, TKey> : BasicAggregateRoot<TEntity, TKey>, IVersion
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化 <see cref="AggregateRoot{TEntity,TKey}"/> 的实例，并使用默认标识。
    /// </summary>
    protected AggregateRoot() : this(default) { }

    /// <summary>
    /// 使用指定标识初始化 <see cref="AggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected AggregateRoot(TKey id) : base(id)
    {
    }

    /// <inheritdoc />
    [DisableAuditing]
    public virtual byte[] Version { get; set; }
}
