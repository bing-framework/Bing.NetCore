using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供创建、修改和删除审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class FullAuditedEntity<TEntity> : AuditedEntity<TEntity>, IFullAuditedObject
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体是否已标记为删除。
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 获取或设置实体被标记为删除的时间。
    /// </summary>
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 获取或设置标记该实体为删除的用户标识。
    /// </summary>
    public virtual Guid? DeleterId { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供完整审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class FullAuditedEntity<TEntity, TKey> : AuditedEntity<TEntity, TKey>, IFullAuditedObject<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置实体是否已标记为删除。
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 获取或设置实体被标记为删除的时间。
    /// </summary>
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 获取或设置标记该实体为删除的用户标识。
    /// </summary>
    public virtual TKey DeleterId { get; set; }

    /// <summary>
    /// 初始化 <see cref="FullAuditedEntity{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected FullAuditedEntity()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="FullAuditedEntity{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected FullAuditedEntity(TKey id) : base(id)
    {
    }
}