using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Bing.Aspects;
using Bing.Data.Queries;
using Bing.DependencyInjection;
using Bing.Domain.Entities;

namespace Bing.Data
{
    /// <summary>
    /// 查询存储器
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    public interface IQueryStore<TEntity> : IQueryStore<TEntity, Guid> where TEntity : class, IKey<Guid> { }

    /// <summary>
    /// 查询存储器
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    [IgnoreDependency]
    public interface IQueryStore<TEntity, in TKey> : IScopedDependency
        where TEntity : class, IKey<TKey>
    {
        #region FindQueryable(获取查询对象)

        /// <summary>
        /// 获取未跟踪查询对象
        /// </summary>
        IQueryable<TEntity> FindAsNoTracking();

        /// <summary>
        /// 获取查询对象
        /// </summary>
        IQueryable<TEntity> Find();

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="criteria">查询条件</param>
        IQueryable<TEntity> Find(ICriteria<TEntity> criteria);

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="predicate">查询条件</param>
        IQueryable<TEntity> Find([NotNull] Expression<Func<TEntity, bool>> predicate);

        #endregion

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

        /// <summary>
        /// 查找未跟踪单个实体
        /// </summary>
        /// <param name="id">标识</param>
        TEntity FindByIdNoTracking(TKey id);

        /// <summary>
        /// 查找未跟踪单个实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<TEntity> FindByIdNoTrackingAsync(TKey id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        List<TEntity> FindByIdsNoTracking(params TKey[] ids);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        List<TEntity> FindByIdsNoTracking(IEnumerable<TKey> ids);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        List<TEntity> FindByIdsNoTracking(string ids);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        Task<List<TEntity>> FindByIdsNoTrackingAsync(params TKey[] ids);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<List<TEntity>> FindByIdsNoTrackingAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        Task<List<TEntity>> FindByIdsNoTrackingAsync(string ids);

        #endregion

        #region Single(查找单个实体)

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        TEntity Single(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

        #endregion

        #region FindAll(查找实体列表)

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="predicate">查询条件</param>
        List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="predicate">查询条件</param>
        Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="predicate">查询条件</param>
        List<TEntity> FindAllNoTracking(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="predicate">查询条件</param>
        Task<List<TEntity>> FindAllNoTrackingAsync(Expression<Func<TEntity, bool>> predicate = null);

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

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        bool Exists([NotNull] Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        Task<bool> ExistsAsync([NotNull] Expression<Func<TEntity, bool>> predicate);

        #endregion

        #region Count(查找数量)

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <param name="predicate">查询条件</param>
        int Count(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <param name="predicate">查询条件</param>
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null);

        #endregion

        #region PageQuery(分页查询)

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        List<TEntity> Query(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        List<TEntity> QueryAsNoTracking(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        PagerList<TEntity> PagerQuery(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        PagerList<TEntity> PagerQueryAsNoTracking(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        Task<List<TEntity>> QueryAsync(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        Task<List<TEntity>> QueryAsNoTrackingAsync(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        Task<PagerList<TEntity>> PagerQueryAsync(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync(IQueryBase<TEntity> query);

        #endregion
    }
}
