using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 创建审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedAggregateRootWithName<TEntity> : CreationAuditedAggregateRoot<TEntity>, ICreationAuditedObjectWithName
        where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public virtual string Creator { get; set; }
    }

    /// <summary>
    /// 创建审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedAggregateRootWithName<TEntity, TKey> : CreationAuditedAggregateRoot<TEntity, TKey>, ICreationAuditedObjectWithName<TKey>
        where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public virtual string Creator { get; set; }

        /// <summary>
        /// 初始化一个<see cref="CreationAuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected CreationAuditedAggregateRootWithName() : this(default)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="CreationAuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected CreationAuditedAggregateRootWithName(TKey id) : base(id)
        {
        }
    }
}
