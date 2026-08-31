using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供创建审计字段和创建人名称。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class CreationAuditedAggregateRootWithName<TEntity> : CreationAuditedAggregateRoot<TEntity>, ICreationAuditedObjectWithName
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该聚合根的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供创建人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class CreationAuditedAggregateRootWithName<TEntity, TKey> : CreationAuditedAggregateRoot<TEntity, TKey>, ICreationAuditedObjectWithName<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该聚合根的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 初始化 <see cref="CreationAuditedAggregateRootWithName{TEntity,TKey}"/> 的实例，并使用默认标识。
    /// </summary>
    protected CreationAuditedAggregateRootWithName() : this(default)
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="CreationAuditedAggregateRootWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected CreationAuditedAggregateRootWithName(TKey id) : base(id)
    {
    }
}