using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供创建和最后修改审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class AuditedAggregateRoot<TEntity> : CreationAuditedAggregateRoot<TEntity>, IAuditedObject
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置聚合根最后一次修改的时间。
    /// </summary>
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 获取或设置最后修改该聚合根的用户标识。
    /// </summary>
    public virtual Guid? LastModifierId { get; set; }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供创建和修改审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class AuditedAggregateRoot<TEntity, TKey> : CreationAuditedAggregateRoot<TEntity, TKey>, IAuditedObject<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置聚合根最后一次修改的时间。
    /// </summary>
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 获取或设置最后修改该聚合根的用户标识。
    /// </summary>
    public virtual TKey LastModifierId { get; set; }

    /// <summary>
    /// 初始化 <see cref="AuditedAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected AuditedAggregateRoot()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="AuditedAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected AuditedAggregateRoot(TKey id) : base(id)
    {
    }
}