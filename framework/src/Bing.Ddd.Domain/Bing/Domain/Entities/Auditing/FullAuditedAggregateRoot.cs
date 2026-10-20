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
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置聚合根是否已标记为删除。
    /// </remarks>
    public virtual bool IsDeleted { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置聚合根被标记为删除的时间。
    /// </remarks>
    public virtual DateTime? DeletionTime { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置标记该聚合根为删除的用户标识。
    /// </remarks>
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
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置聚合根是否已标记为删除。
    /// </remarks>
    public virtual bool IsDeleted { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置聚合根被标记为删除的时间。
    /// </remarks>
    public virtual DateTime? DeletionTime { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置标记该聚合根为删除的用户标识。
    /// </remarks>
    public virtual TKey DeleterId { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="FullAuditedAggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    protected FullAuditedAggregateRoot()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="FullAuditedAggregateRoot{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="id">聚合根标识。</param>
    protected FullAuditedAggregateRoot(TKey id) : base(id)
    {
    }
}