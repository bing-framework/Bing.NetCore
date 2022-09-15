using System;
using Bing.Auditing;
using Bing.Validation;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class AuditedEntity<TEntity> : CreationAuditedEntity<TEntity>, IAuditedObject
        where TEntity : class, IEntity, IVerifyModel<TEntity>
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
    /// 审计实体
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class AuditedEntity<TEntity, TKey> : CreationAuditedEntity<TEntity, TKey>, IAuditedObject<TKey>
        where TEntity : class, IEntity, IVerifyModel<TEntity>
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
        /// 初始化一个<see cref="AuditedEntity{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected AuditedEntity()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="AuditedEntity{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AuditedEntity(TKey id) : base(id)
        {
        }
    }
}
