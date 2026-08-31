using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供创建和最后修改审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class AuditedEntity<TEntity> : CreationAuditedEntity<TEntity>, IAuditedObject
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体最后一次修改的时间。
    /// </summary>
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 获取或设置最后修改该实体的用户标识。
    /// </summary>
    public virtual Guid? LastModifierId { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供创建和修改审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class AuditedEntity<TEntity, TKey> : CreationAuditedEntity<TEntity, TKey>, IAuditedObject<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体最后一次修改的时间。
    /// </summary>
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 获取或设置最后修改该实体的用户标识。
    /// </summary>
    public virtual TKey LastModifierId { get; set; }

    /// <summary>
    /// 初始化 <see cref="AuditedEntity{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected AuditedEntity()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="AuditedEntity{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected AuditedEntity(TKey id) : base(id)
    {
    }
}