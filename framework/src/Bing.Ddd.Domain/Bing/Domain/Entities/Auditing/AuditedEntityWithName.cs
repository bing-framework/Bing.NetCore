using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class AuditedEntityWithName<TEntity> : CreationAuditedEntityWithName<TEntity>, IAuditedObjectWithName
        where TEntity : class, IEntity
    {
        /// <summary>
        /// 最后修改时间
        /// </summary>
        public virtual DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public virtual Guid? LastModifierId { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        public virtual string LastModifier { get; set; }
    }

    /// <summary>
    /// 审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class AuditedEntityWithName<TEntity, TKey> : CreationAuditedEntityWithName<TEntity, TKey>, IAuditedObjectWithName<TKey>
        where TEntity : class, IEntity
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
        /// 最后修改人
        /// </summary>
        public virtual string LastModifier { get; set; }

        /// <summary>
        /// 初始化一个<see cref="AuditedEntityWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected AuditedEntityWithName()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="AuditedEntityWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AuditedEntityWithName(TKey id) : base(id)
        {
        }
    }
}
