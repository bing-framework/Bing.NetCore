using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 为实体提供创建人和最后修改人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
[Serializable]
public abstract class AuditedEntityWithName<TEntity> : AuditedEntity<TEntity>, IAuditedObjectWithName
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该实体的用户名称。
    /// </remarks>
    public virtual string Creator { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置最后修改该实体的用户名称。
    /// </remarks>
    public virtual string LastModifier { get; set; }
}

/// <summary>
/// 为具有指定标识类型的实体提供创建人和修改人名称审计字段。
/// </summary>
/// <typeparam name="TEntity">具体实体类型。</typeparam>
/// <typeparam name="TKey">实体标识类型。</typeparam>
[Serializable]
public abstract class AuditedEntityWithName<TEntity, TKey> : AuditedEntity<TEntity, TKey>, IAuditedObjectWithName<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置创建该实体的用户名称。
    /// </remarks>
    public virtual string Creator { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 获取或设置最后修改该实体的用户名称。
    /// </remarks>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 初始化一个 <see cref="AuditedEntityWithName{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    protected AuditedEntityWithName()
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="AuditedEntityWithName{TEntity, TKey}"/> 类型的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    protected AuditedEntityWithName(TKey id) : base(id)
    {
    }
}