using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供创建时间、创建人标识和创建人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class CreationAuditedEntityWithName<TEntity> : CreationAuditedEntity<TEntity>, ICreationAuditedObjectWithName
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该实体的用户名称。
    /// </remarks>
    public virtual string Creator { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供创建人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class CreationAuditedEntityWithName<TEntity, TKey> : CreationAuditedEntity<TEntity, TKey>, ICreationAuditedObjectWithName<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该实体的用户名称。
    /// </remarks>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedEntityWithName{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    protected CreationAuditedEntityWithName()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CreationAuditedEntityWithName{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected CreationAuditedEntityWithName(TKey id) : base(id)
    {
    }
}