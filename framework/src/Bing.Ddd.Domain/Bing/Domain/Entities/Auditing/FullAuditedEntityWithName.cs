using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供完整审计字段及创建人、修改人和删除人名称。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class FullAuditedEntityWithName<TEntity> : FullAuditedEntity<TEntity>, IFullAuditedObjectWithName
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该实体的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 获取或设置最后修改该实体的用户名称。
    /// </summary>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 获取或设置标记该实体为删除的用户名称。
    /// </summary>
    public virtual string Deleter { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供完整审计字段及人员名称。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class FullAuditedEntityWithName<TEntity, TKey> : FullAuditedEntity<TEntity, TKey>, IFullAuditedObjectWithName<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置创建该实体的用户名称。
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 获取或设置最后修改该实体的用户名称。
    /// </summary>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 获取或设置标记该实体为删除的用户名称。
    /// </summary>
    public virtual string Deleter { get; set; }

    /// <summary>
    /// 初始化 <see cref="FullAuditedEntityWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected FullAuditedEntityWithName()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="FullAuditedEntityWithName{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected FullAuditedEntityWithName(TKey id) : base(id)
    {
    }
}