using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 完整审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class FullAuditedEntity<TEntity> : AuditedEntity<TEntity>, IFullAuditedObject
        where TEntity : class, IEntity, IVerifyModel<TEntity>
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
    /// 完整审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class FullAuditedEntity<TEntity, TKey> : AuditedEntity<TEntity, TKey>, IFullAuditedObject<TKey>
        where TEntity : class, IEntity, IVerifyModel<TEntity>
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
        /// 初始化一个<see cref="FullAuditedEntity{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected FullAuditedEntity()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="FullAuditedEntity{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected FullAuditedEntity(TKey id) : base(id)
        {
        }
    }
}
