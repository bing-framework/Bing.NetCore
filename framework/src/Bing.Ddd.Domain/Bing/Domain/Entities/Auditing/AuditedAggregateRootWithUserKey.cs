using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体主键类型与用户标识类型不同的聚合根提供创建和修改审计字段。
/// </summary>
/// <typeparam name="TEntity">具体聚合根类型。</typeparam>
/// <typeparam name="TKey">聚合根主键类型。</typeparam>
/// <typeparam name="TUserKey">审计用户标识类型。</typeparam>
[Serializable]
public abstract class AuditedAggregateRootWithUserKey<TEntity, TKey, TUserKey> : CreationAuditedAggregateRootWithUserKey<TEntity, TKey, TUserKey>, IAuditedObject<TUserKey?>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
    where TUserKey : struct
{
    /// <inheritdoc />
    public virtual DateTime? LastModificationTime { get; set; }

    /// <inheritdoc />
    /// <remarks>该字段使用用户标识类型，不受聚合根主键类型影响。</remarks>
    public virtual TUserKey? LastModifierId { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="AuditedAggregateRootWithUserKey{TEntity,TKey,TUserKey}" /> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 初始化一个使用默认主键的 <see cref="AuditedAggregateRootWithUserKey{TEntity, TKey, TUserKey}" />。
    /// </remarks>
    protected AuditedAggregateRootWithUserKey()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="AuditedAggregateRootWithUserKey{TEntity,TKey,TUserKey}" /> 类型的实例。
    /// </summary>
    /// <param name="id">聚合根主键。</param>
    /// <remarks>
    /// 使用指定主键初始化创建和修改审计聚合根。
    /// </remarks>
    protected AuditedAggregateRootWithUserKey(TKey id) : base(id)
    {
    }
}
