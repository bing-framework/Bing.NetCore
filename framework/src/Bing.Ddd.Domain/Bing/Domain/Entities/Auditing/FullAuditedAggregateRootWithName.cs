using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing;

/// <summary>
/// 完整审计聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRootWithName<TEntity> : FullAuditedAggregateRoot<TEntity>, IFullAuditedObjectWithName
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
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
/// 完整审计聚合根
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">标识类型</typeparam>
[Serializable]
public abstract class FullAuditedAggregateRootWithName<TEntity, TKey> : FullAuditedAggregateRoot<TEntity, TKey>, IFullAuditedObjectWithName<TKey>
    where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
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
    /// 初始化一个<see cref="FullAuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
    /// </summary>
    protected FullAuditedAggregateRootWithName()
    {
    }

    /// <summary>
    /// 初始化一个<see cref="FullAuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    protected FullAuditedAggregateRootWithName(TKey id) : base(id)
    {
    }
}