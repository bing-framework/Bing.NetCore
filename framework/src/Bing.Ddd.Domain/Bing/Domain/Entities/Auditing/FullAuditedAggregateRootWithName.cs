using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供完整审计字段及创建人、修改人和删除人名称。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRootWithName<TEntity> : FullAuditedAggregateRoot<TEntity>, IFullAuditedObjectWithName
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
    /// 获取或设置标记该聚合根为删除的用户名称。
    /// </summary>
    public virtual string Deleter { get; set; }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供完整审计字段及人员名称。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRootWithName<TEntity, TKey> : FullAuditedAggregateRoot<TEntity, TKey>, IFullAuditedObjectWithName<TKey>
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
    /// 获取或设置标记该聚合根为删除的用户名称。
    /// </summary>
    public virtual string Deleter { get; set; }

    /// <summary>
    /// 初始化 <see cref="FullAuditedAggregateRootWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected FullAuditedAggregateRootWithName()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="FullAuditedAggregateRootWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected FullAuditedAggregateRootWithName(TKey id) : base(id)
    {
    }
}