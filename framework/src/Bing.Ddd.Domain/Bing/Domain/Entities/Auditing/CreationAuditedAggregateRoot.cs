using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供创建时间和创建人标识审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class CreationAuditedAggregateRoot<TEntity> : AggregateRoot<TEntity>, ICreationAuditedObject
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    public virtual DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该聚合根的用户标识。
    /// </remarks>
    public virtual Guid? CreatorId { get; set; }

}

/// <summary>
/// 为具有指定标识类型的聚合根提供创建审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class CreationAuditedAggregateRoot<TEntity, TKey> : AggregateRoot<TEntity, TKey>, ICreationAuditedObject<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    public virtual DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该聚合根的用户标识。
    /// </remarks>
    public virtual TKey CreatorId { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedAggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    protected CreationAuditedAggregateRoot() : this(default)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedAggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected CreationAuditedAggregateRoot(TKey id) : base(id)
    {
    }
}