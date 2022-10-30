using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 创建审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
[Serializable]
public abstract class CreationAuditedEntity<TEntity> : EntityBase<TEntity>, ICreationAuditedObject
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    public virtual Guid? CreatorId { get; set; }
}

/// <summary>
/// 创建审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
[Serializable]
public abstract class CreationAuditedEntity<TEntity, TKey> : EntityBase<TEntity, TKey>, ICreationAuditedObject<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建时间
    /// </summary>
    public virtual DateTime? CreationTime { get; set; }

    /// <summary>
    /// 创建人标识
    /// </summary>
    public virtual TKey CreatorId { get; set; }

    /// <summary>
    /// 初始化一个<see cref="CreationAuditedEntity{TEntity, TKey}"/>类型的实例
    /// </summary>
    protected CreationAuditedEntity()
    {
    }

    /// <summary>
    /// 初始化一个<see cref="CreationAuditedEntity{TEntity, TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected CreationAuditedEntity(TKey id) : base(id)
    {
    }
}