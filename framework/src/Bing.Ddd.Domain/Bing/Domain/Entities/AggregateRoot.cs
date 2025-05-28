using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities;

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
public abstract class AggregateRoot<TEntity> : BasicAggregateRoot<TEntity>, IVersion
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化一个<see cref="AggregateRoot{TEntity}"/>类型的实例
    /// </summary>
    protected AggregateRoot() : this(Guid.Empty)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="AggregateRoot{TEntity}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// 版本号（乐观锁）
    /// </summary>
    [DisableAuditing]
    public virtual byte[] Version { get; set; }
}

/// <summary>
/// 聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
public abstract class AggregateRoot<TEntity, TKey> : BasicAggregateRoot<TEntity, TKey>, IVersion
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
    /// </summary>
    protected AggregateRoot() : this(default) { }

    /// <summary>
    /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected AggregateRoot(TKey id) : base(id)
    {
    }

    /// <summary>
    /// 版本号（乐观锁）
    /// </summary>
    [DisableAuditing]
    public virtual byte[] Version { get; set; }
}
