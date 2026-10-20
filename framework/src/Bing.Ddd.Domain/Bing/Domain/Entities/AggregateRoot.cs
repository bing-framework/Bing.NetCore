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
    /// 初始化一个 <see cref="AggregateRoot{TEntity}"/> 类型的实例。
    /// </summary>
    protected AggregateRoot() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="AggregateRoot{TEntity}"/> 类型的实例。
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
    /// 初始化一个 <see cref="AggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    protected AggregateRoot() : this(default) { }

    /// <summary>
    /// 初始化一个 <see cref="AggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected AggregateRoot(TKey id) : base(id)
    {
    }

    /// <inheritdoc />
    [DisableAuditing]
    public virtual byte[] Version { get; set; }
}
