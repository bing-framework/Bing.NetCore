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
    public abstract class CreationAuditedAggregateRoot<TEntity> : AggregateRoot<TEntity>, ICreationAuditedObject
        where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
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
    public abstract class CreationAuditedAggregateRoot<TEntity, TKey> : AggregateRoot<TEntity, TKey>, ICreationAuditedObject<TKey>
        where TEntity : class, IAggregateRoot, IVerifyModel<TEntity>
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
        /// 初始化一个<see cref="CreationAuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected CreationAuditedAggregateRoot() : this(default)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="CreationAuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected CreationAuditedAggregateRoot(TKey id) : base(id)
        {
        }
    }
}
