using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体主键类型与用户标识类型不同的聚合根提供完整审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根主键类型。</typeparam>
/// <typeparam name="TUserKey">审计用户标识类型。</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRootWithUserKey<TEntity, TKey, TUserKey> : AuditedAggregateRootWithUserKey<TEntity, TKey, TUserKey>, IFullAuditedObject<TUserKey?>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
    where TUserKey : struct
{
    /// <inheritdoc />
    public virtual bool IsDeleted { get; set; }

    /// <inheritdoc />
    public virtual DateTime? DeletionTime { get; set; }

    /// <inheritdoc />
    /// <remarks>该字段使用用户标识类型，不受聚合根主键类型影响。</remarks>
    public virtual TUserKey? DeleterId { get; set; }

    /// <summary>
    /// 初始化一个使用默认主键的 <see cref="FullAuditedAggregateRootWithUserKey{TEntity, TKey, TUserKey}"/>。
    /// </summary>
    protected FullAuditedAggregateRootWithUserKey()
    {
    }

    /// <summary>
    /// 使用指定主键初始化完整审计聚合根。
    /// </summary>
    /// <param name="id">聚合根主键。</param>
    protected FullAuditedAggregateRootWithUserKey(TKey id) : base(id)
    {
    }
}
