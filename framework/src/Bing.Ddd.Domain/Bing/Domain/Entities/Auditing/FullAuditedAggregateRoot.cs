using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 完整审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class FullAuditedAggregateRoot<TEntity> : AuditedAggregateRoot<TEntity>, IFullAuditedObject
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 是否已删除
        /// </summary>
        public virtual bool IsDeleted { get; set; }

        /// <summary>
        /// 删除时间
        /// </summary>
        public virtual DateTime? DeletionTime { get; set; }

        /// <summary>
        /// 删除人标识
        /// </summary>
        public virtual Guid? DeleterId { get; set; }
    }

    /// <summary>
    /// 完整审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class FullAuditedAggregateRoot<TEntity, TKey> : AuditedAggregateRoot<TEntity, TKey>, IFullAuditedObject<TKey>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 是否已删除
        /// </summary>
        public virtual bool IsDeleted { get; set; }

        /// <summary>
        /// 删除时间
        /// </summary>
        public virtual DateTime? DeletionTime { get; set; }

        /// <summary>
        /// 删除人标识
        /// </summary>
        public virtual TKey DeleterId { get; set; }

        /// <summary>
        /// 初始化一个<see cref="FullAuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected FullAuditedAggregateRoot()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="FullAuditedAggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected FullAuditedAggregateRoot(TKey id) : base(id)
        {
        }
    }
}
