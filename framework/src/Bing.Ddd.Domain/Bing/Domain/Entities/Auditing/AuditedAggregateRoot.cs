using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class AuditedAggregateRoot<TEntity> : CreationAuditedAggregateRoot<TEntity>, IAuditedObject
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public virtual DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public virtual Guid? LastModifierId { get; set; }
    }

    /// <summary>
    /// 审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class AuditedAggregateRoot<TEntity, TKey> : CreationAuditedAggregateRoot<TEntity, TKey>, IAuditedObject<TKey>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public virtual DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public virtual TKey LastModifierId { get; set; }

        /// <summary>
        /// 初始化一个<see cref="AuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected AuditedAggregateRoot()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="AuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AuditedAggregateRoot(TKey id) : base(id)
        {
        }
    }
}
