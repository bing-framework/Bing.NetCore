using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为聚合根提供创建、修改和删除审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRoot<TEntity> : AuditedAggregateRoot<TEntity>, IFullAuditedObject
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置聚合根是否已标记为删除。
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 获取或设置聚合根被标记为删除的时间。
    /// </summary>
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 获取或设置标记该聚合根为删除的用户标识。
    /// </summary>
    public virtual Guid? DeleterId { get; set; }
}

/// <summary>
/// 为具有指定标识类型的聚合根提供完整审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根标识类型。</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRoot<TEntity, TKey> : AuditedAggregateRoot<TEntity, TKey>, IFullAuditedObject<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
{
    /// <summary>
    /// 获取或设置聚合根是否已标记为删除。
    /// </summary>
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 获取或设置聚合根被标记为删除的时间。
    /// </summary>
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 获取或设置标记该聚合根为删除的用户标识。
    /// </summary>
    public virtual TKey DeleterId { get; set; }

    /// <summary>
    /// 初始化 <see cref="FullAuditedAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    protected FullAuditedAggregateRoot()
    {
    }

    /// <summary>
    /// 使用指定标识初始化 <see cref="FullAuditedAggregateRoot{TEntity,TKey}"/> 的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected FullAuditedAggregateRoot(TKey id) : base(id)
    {
    }
}