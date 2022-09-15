using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Validation;

namespace Bing.Domain.Repositories
{
    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public interface ICompactRepository<TEntity> : ICompactRepository<TEntity, Guid>
        where TEntity : class, IAggregateRoot, IKey<Guid>, IVersion
    {
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    [IgnoreDependency]
    public interface ICompactRepository<TEntity, in TKey> : IScopedDependency
        where TEntity : class, IAggregateRoot, IKey<TKey>, IVersion
    {
        #region FindById(通过标识查找)

        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">标识</param>
        TEntity Find(object id);

        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<TEntity> FindAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        List<TEntity> FindByIds(params TKey[] ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        List<TEntity> FindByIds(IEnumerable<TKey> ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        List<TEntity> FindByIds(string ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        Task<List<TEntity>> FindByIdsAsync(params TKey[] ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        Task<List<TEntity>> FindByIdsAsync(string ids);

        #endregion

        #region Exists(判断是否存在)

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="id">标识</param>
        bool Exists(TKey id);

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="ids">标识列表</param>
        bool Exists(TKey[] ids);

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="id">标识</param>
        Task<bool> ExistsAsync(TKey id);

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="ids">标识列表</param>
        Task<bool> ExistsAsync(TKey[] ids);

        #endregion

        #region Add(添加)

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        void Add([Valid] TEntity entity);

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        void Add([Valid] IEnumerable<TEntity> entities);

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task AddAsync([Valid] TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        #endregion

        #region Update(修改)

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        void Update([Valid] TEntity entity);

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        void Update([Valid] IEnumerable<TEntity> entities);

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        Task UpdateAsync([Valid] TEntity entity);

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        Task UpdateAsync([Valid] IEnumerable<TEntity> entities);

        #endregion

        #region Remove(移除)

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        void Remove(object id);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        void Remove(TEntity entity);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        void Remove(IEnumerable<TKey> ids);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        void Remove(IEnumerable<TEntity> entities);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task RemoveAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task RemoveAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task RemoveAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        #endregion
    }
}
