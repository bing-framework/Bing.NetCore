using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Domain.Entities;
using FreeSql;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.Data.Stores
{
    /// <summary>
    /// 存储器
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    public abstract class StoreBase<TEntity> : StoreBase<TEntity, Guid>, IStore<TEntity>
        where TEntity : class, IKey<Guid>, IVersion
    {
        /// <summary>
        /// 初始化一个<see cref="StoreBase{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected StoreBase(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }

    /// <summary>
    /// 存储器
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    public abstract class StoreBase<TEntity, TKey> : QueryStoreBase<TEntity, TKey>, IStore<TEntity, TKey> where TEntity : class, IKey<TKey>
    {
        /// <summary>
        /// 初始化一个<see cref="StoreBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected StoreBase(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual void Add(TEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            Set.Add(entity);
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual void Add(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            Set.AddRange(entities);
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            await Set.AddAsync(entity);
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            await Set.AddRangeAsync(entities);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual void Update(TEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            Set.Update(entity);
        }

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual void Update(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            Set.UpdateRangeAsync(entities);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        public virtual Task UpdateAsync(TEntity entity)
        {
            Update(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <exception cref="ArgumentNullException"></exception>
        public virtual async Task UpdateAsync(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));
            await Set.UpdateRangeAsync(entities);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        public virtual void Remove(object id)
        {
            var entity = Find(id);
            Delete(entity);
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="entity">实体</param>
        private void Delete(TEntity entity)
        {
            if (entity == null)
                return;
            if (entity is ISoftDelete model)
            {
                model.IsDeleted = true;
                Set.Update(entity);
                return;
            }

            Set.Remove(entity);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        public virtual void Remove(TEntity entity)
        {
            if (entity == null)
                return;
            Remove(entity.Id);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        public virtual void Remove(IEnumerable<TKey> ids)
        {
            if (ids == null)
                return;
            var list = FindByIds(ids);
            Delete(list);
        }

        /// <summary>
        /// 删除实体集合
        /// </summary>
        /// <param name="list">实体集合</param>
        private void Delete(List<TEntity> list)
        {
            if (list == null)
                return;
            if (!list.Any())
                return;
            if (list[0] is ISoftDelete)
            {
                foreach (var entity in list.Select(t => (ISoftDelete)t))
                    entity.IsDeleted = true;
                Set.UpdateRange(list);
                return;
            }
            Set.RemoveRange(list);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public virtual void Remove(IEnumerable<TEntity> entities)
        {
            if (entities == null)
                return;
            Remove(entities.Select(t => t.Id));
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        public virtual async Task RemoveAsync(object id, CancellationToken cancellationToken = default)
        {
            var entity = await FindAsync(id, cancellationToken);
            Delete(entity);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        public virtual async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                return;
            await RemoveAsync(entity.Id, cancellationToken);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public virtual async Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            if (ids == null)
                return;
            var entities = await FindByIdsAsync(ids, cancellationToken);
            Delete(entities);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public virtual async Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null)
                return;
            await RemoveAsync(entities.Select(t => t.Id), cancellationToken);
        }
    }
}
