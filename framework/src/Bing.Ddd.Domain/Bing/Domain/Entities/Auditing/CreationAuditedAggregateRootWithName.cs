using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 创建审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedAggregateRootWithName<TEntity> : AggregateRoot<TEntity>, ICreationAuditedObject
        where TEntity : class, IAggregateRoot
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
    /// 创建审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class CreationAuditedAggregateRootWithName<TEntity, TKey> : AggregateRoot<TEntity, TKey>, ICreationAuditedObject<TKey>
        where TEntity : class, IAggregateRoot
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
