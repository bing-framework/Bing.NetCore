using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供创建人和最后修改人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class AuditedAggregateRootWithName<TEntity> : AuditedAggregateRoot<TEntity>, IAuditedObjectWithName
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该聚合根的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 获取或设置最后修改该聚合根的用户名称。
    /// </summary>
    public virtual string LastModifier { get; set; }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供创建人和修改人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class AuditedAggregateRootWithName<TEntity, TKey> : AuditedAggregateRoot<TEntity, TKey>, IAuditedObjectWithName<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该聚合根的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 获取或设置最后修改该聚合根的用户名称。
    /// </summary>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 初始化 <see cref="AuditedAggregateRootWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected AuditedAggregateRootWithName()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="AuditedAggregateRootWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected AuditedAggregateRootWithName(TKey id) : base(id)
    {
    }
}