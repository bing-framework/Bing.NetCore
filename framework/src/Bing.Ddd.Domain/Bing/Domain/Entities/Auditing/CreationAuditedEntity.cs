using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 创建审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class CreationAuditedEntity<TEntity> : EntityBase<TEntity>, ICreationAuditedObject
        where TEntity : class, IEntity
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
    public abstract class CreationAuditedEntity<TEntity, TKey> : EntityBase<TEntity, TKey>, ICreationAuditedObject<TKey>
        where TEntity : class, IEntity
    {
        /// <summary>
        /// 创建时间
        /// </summary>
        public virtual DateTime? CreationTime { get; set; }

        /// <summary>
        /// 创建人标识
        /// </summary>
        public virtual TKey CreatorId { get; set; }
    }
}
