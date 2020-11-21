using System;
using System.Collections.Generic;
using Bing.Auditing;
using Bing.Domain.Entities.Events;

namespace Bing.Domain.Entities
{
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

    /// <summary>
    /// 聚合根
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">标识类型</typeparam>
    public abstract class AggregateRoot<TEntity, TKey> : EntityBase<TEntity, TKey>, IAggregateRoot<TEntity, TKey>
        where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 领域事件列表
        /// </summary>
        private List<DomainEvent> _domainEvents;

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

        /// <summary>
        /// 获取领域事件集合
        /// </summary>
        public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents?.AsReadOnly();

        /// <summary>
        /// 添加领域事件
        /// </summary>
        /// <param name="event">领域事件</param>
        public void AddDomainEvent(DomainEvent @event)
        {
            _domainEvents ??= new List<DomainEvent>();
            _domainEvents.Add(@event);
        }

        /// <summary>
        /// 移除领域事件
        /// </summary>
        /// <param name="event">领域事件</param>
        public void RemoveDomainEvent(DomainEvent @event) => _domainEvents?.Remove(@event);

        /// <summary>
        /// 清空领域事件
        /// </summary>
        public void ClearDomainEvents() => _domainEvents?.Clear();
    }
}
