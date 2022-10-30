using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 创建审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
[Serializable]
public abstract class CreationAuditedEntityWithName<TEntity> : CreationAuditedEntity<TEntity>, ICreationAuditedObjectWithName
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string Creator { get; set; }
}

/// <summary>
/// 创建审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
[Serializable]
public abstract class CreationAuditedEntityWithName<TEntity, TKey> : CreationAuditedEntity<TEntity, TKey>, ICreationAuditedObjectWithName<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 初始化一个<see cref="CreationAuditedEntityWithName{TEntity, TKey}"/>类型的实例
    /// </summary>
    protected CreationAuditedEntityWithName()
    {
    }

    /// <summary>
    /// 初始化一个<see cref="CreationAuditedEntityWithName{TEntity, TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected CreationAuditedEntityWithName(TKey id) : base(id)
    {
    }
}