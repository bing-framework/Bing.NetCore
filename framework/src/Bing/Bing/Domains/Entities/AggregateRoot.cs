using System;
using Bing.Auditing;

namespace Bing.Domains.Entities
{
    /// <summary>
    /// 聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    public abstract class AggregateRoot<TEntity, TKey> : EntityBase<TEntity, TKey>, IAggregateRoot<TEntity, TKey>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        protected AggregateRoot() : this(default) { }

        /// <summary>
        /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AggregateRoot(TKey id) : base(id)
        {
        }

        /// <summary>
        /// 版本号（乐观锁）
        /// </summary>
        [DisableAuditing]
        public byte[] Version { get; set; }
    }

    /// <summary>
    /// 聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class AggregateRoot<TEntity> : AggregateRoot<TEntity, Guid>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 初始化一个<see cref="AggregateRoot{TEntity}"/>类型的实例
        /// </summary>
        protected AggregateRoot() : this(Guid.Empty)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="AggregateRoot{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected AggregateRoot(Guid id) : base(id)
        {
        }
    }
}
