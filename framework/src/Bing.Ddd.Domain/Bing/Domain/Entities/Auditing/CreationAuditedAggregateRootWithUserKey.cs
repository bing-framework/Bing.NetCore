using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体主键类型与用户标识类型不同的聚合根提供创建审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根主键类型。</typeparam>
/// <typeparam name="TUserKey">创建用户标识类型。</typeparam>
[Serializable]
public abstract class CreationAuditedAggregateRootWithUserKey<TEntity, TKey, TUserKey> : AggregateRoot<TEntity, TKey>, ICreationAuditedObject<TUserKey?>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
    where TUserKey : struct
{
    /// <inheritdoc />
    public virtual DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    /// <remarks>该字段使用用户标识类型，不受聚合根主键类型影响。</remarks>
    public virtual TUserKey? CreatorId { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedAggregateRootWithUserKey{TEntity,TKey,TUserKey}" /> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 初始化一个使用默认主键的 <see cref="CreationAuditedAggregateRootWithUserKey{TEntity, TKey, TUserKey}" />。
    /// </remarks>
    protected CreationAuditedAggregateRootWithUserKey() : this(default)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedAggregateRootWithUserKey{TEntity,TKey,TUserKey}" /> 类型的实例。
    /// </summary>
    /// <param name="id">聚合根主键。</param>
    /// <remarks>
    /// 使用指定主键初始化创建审计聚合根。
    /// </remarks>
    protected CreationAuditedAggregateRootWithUserKey(TKey id) : base(id)
    {
    }
}
