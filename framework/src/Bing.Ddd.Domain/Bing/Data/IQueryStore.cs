using System.Linq.Expressions;
using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Domain.Entities;

namespace Bing.Data;

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
public interface IQueryStore<TEntity, in TKey> : IScopedDependency, IDisposable
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
    IQueryable<TEntity> Find(ICondition<TEntity> criteria);

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
    [Obsolete("请使用 FindById 方法")]
    TEntity Find(object id);

    /// <summary>
    /// 通过标识查找实体
    /// </summary>
    /// <param name="id">标识</param>
    TEntity FindById(object id);

    /// <summary>
    /// 通过标识查找实体
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    [Obsolete("请使用 FindByIdAsync 方法")]
    Task<TEntity> FindAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识查找实体
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TEntity> FindByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    List<TEntity> FindByIds(params TKey[] ids);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    List<TEntity> FindByIds(IEnumerable<TKey> ids);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
    List<TEntity> FindByIds(string ids);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task<List<TEntity>> FindByIdsAsync(params TKey[] ids);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">标识列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识列表查找实体列表
    /// </summary>
    /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindByIdsAsync(string ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识查找实体，不跟踪
    /// </summary>
    /// <param name="id">标识</param>
    TEntity FindByIdNoTracking(TKey id);

    /// <summary>
    /// 通过标识查找实体，不跟踪
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TEntity> FindByIdNoTrackingAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">标识列表</param>
    List<TEntity> FindByIdsNoTracking(params TKey[] ids);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">标识列表</param>
    List<TEntity> FindByIdsNoTracking(IEnumerable<TKey> ids);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
    List<TEntity> FindByIdsNoTracking(string ids);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">标识列表</param>
    Task<List<TEntity>> FindByIdsNoTrackingAsync(params TKey[] ids);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">标识列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindByIdsNoTrackingAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 通过标识列表查找实体列表，不跟踪
    /// </summary>
    /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindByIdsNoTrackingAsync(string ids, CancellationToken cancellationToken = default);

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
    /// <param name="action">访问IQueryable的回调函数,用于执行Include等操作</param>
    TEntity Single(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找单个实体
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="action">访问IQueryable的回调函数,用于执行Include等操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate, Func<IQueryable<TEntity>, IQueryable<TEntity>> action, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查找实体列表，不跟踪
    /// </summary>
    /// <param name="predicate">查询条件</param>
    List<TEntity> FindAllNoTracking(Expression<Func<TEntity, bool>> predicate = null);

    /// <summary>
    /// 查找实体列表，不跟踪
    /// </summary>
    /// <param name="predicate">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<List<TEntity>> FindAllNoTrackingAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">取消令牌</param>
    Task<bool> ExistsAsync([NotNull] Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

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
    /// <param name="cancellationToken">取消令牌</param>
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);

    #endregion
}
