using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 完整审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
[Serializable]
public abstract class FullAuditedEntityWithName<TEntity> : FullAuditedEntity<TEntity>, IFullAuditedObjectWithName
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 最后修改人
    /// </summary>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 删除人
    /// </summary>
    public virtual string Deleter { get; set; }
}

/// <summary>
/// 完整审计实体
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
[Serializable]
public abstract class FullAuditedEntityWithName<TEntity, TKey> : FullAuditedEntity<TEntity, TKey>, IFullAuditedObjectWithName<TKey>
    where TEntity : class, IEntity, IVerifyModel<TEntity>
{
    /// <summary>
    /// 创建人
    /// </summary>
    public virtual string Creator { get; set; }

    /// <summary>
    /// 最后修改人
    /// </summary>
    public virtual string LastModifier { get; set; }

    /// <summary>
    /// 删除人
    /// </summary>
    public virtual string Deleter { get; set; }

    /// <summary>
    /// 初始化一个<see cref="FullAuditedEntityWithName{TEntity,TKey}"/>类型的实例
    /// </summary>
    protected FullAuditedEntityWithName()
    {
    }

    /// <summary>
    /// 初始化一个<see cref="FullAuditedEntityWithName{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected FullAuditedEntityWithName(TKey id) : base(id)
    {
    }
}