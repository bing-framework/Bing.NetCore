using System;
using Bing.Auditing;

namespace Bing.Domain.Entities.Auditing
{
    /// <summary>
    /// 审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    [Serializable]
    public abstract class AuditedAggregateRootWithName<TEntity> : AuditedAggregateRoot<TEntity>, IAuditedObjectWithName
        where TEntity : class, IAggregateRoot
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
    /// 审计聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    [Serializable]
    public abstract class AuditedAggregateRootWithName<TEntity, TKey> : AuditedAggregateRoot<TEntity, TKey>, IAuditedObjectWithName<TKey>
        where TEntity : class, IAggregateRoot
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
        /// 初始化一个<see cref="AuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected AuditedAggregateRootWithName()
        {
        }

        /// <summary>
        /// 初始化一个<see cref="AuditedAggregateRootWithName{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AuditedAggregateRootWithName(TKey id) : base(id)
        {
        }
    }
}
