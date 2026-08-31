using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供创建时间和创建人标识审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class CreationAuditedEntity<TEntity> : EntityBase<TEntity>, ICreationAuditedObject
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体的创建时间。
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 获取或设置创建该实体的用户标识。
    /// </summary>
    public virtual Guid? CreatorId { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供创建审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class CreationAuditedEntity<TEntity, TKey> : EntityBase<TEntity, TKey>, ICreationAuditedObject<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体的创建时间。
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 获取或设置创建该实体的用户标识。
    /// </summary>
    public virtual TKey CreatorId { get; set; }

    /// <summary>
    /// 初始化 <see cref="CreationAuditedEntity{TEntity, TKey}"/> 的实例。
    /// </summary>
    protected CreationAuditedEntity()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="CreationAuditedEntity{TEntity, TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected CreationAuditedEntity(TKey id) : base(id)
    {
    }
}