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
    /// <summary>
    /// 获取或设置聚合根的创建时间。
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 获取或设置创建该聚合根的用户标识。
    /// </summary>
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
    /// <summary>
    /// 获取或设置聚合根的创建时间。
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 获取或设置创建该聚合根的用户标识。
    /// </summary>
    public virtual TKey CreatorId { get; set; }

    /// <summary>
    /// 初始化 <see cref="CreationAuditedAggregateRoot{TEntity,TKey}"/> 的实例，并使用默认标识。
    /// </summary>
    protected CreationAuditedAggregateRoot() : this(default)
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="CreationAuditedAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected CreationAuditedAggregateRoot(TKey id) : base(id)
    {
    }
}