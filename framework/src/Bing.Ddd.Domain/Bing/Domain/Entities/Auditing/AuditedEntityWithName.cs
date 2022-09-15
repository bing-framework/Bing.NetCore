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
    public abstract class AuditedEntityWithName<TEntity> : AuditedEntity<TEntity>, IAuditedObjectWithName
        where TEntity : class, IEntity, IVerifyModel<TEntity>
    {
        /// <summary>
        /// 创建人
        /// </summary>
        public virtual string Creator { get; set; }

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
    public abstract class AuditedEntityWithName<TEntity, TKey> : AuditedEntity<TEntity, TKey>, IAuditedObjectWithName<TKey>
        where TEntity : class, IEntity, IVerifyModel<TEntity>
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
