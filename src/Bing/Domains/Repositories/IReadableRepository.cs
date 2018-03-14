using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Bing.Dependency;
using Bing.Domains.Entities;

namespace Bing.Domains.Repositories
{
    /// <summary>
    /// 仓储 - 可读
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public interface IReadableRepository<TEntity> : IReadableRepository<TEntity, Guid>
        where TEntity : class, IAggregateRoot
    {
    }

    /// <summary>
    /// 仓储 - 可读
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface IReadableRepository<TEntity, in TKey> : IScopeDependency where TEntity : class, IAggregateRoot
    {
        #region Find(懒加载查找实体集合)

        /// <summary>
        /// 获取未跟踪的实体集
        /// </summary>
        /// <returns></returns>
        IQueryable<TEntity> FindAsNoTracking();

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <returns></returns>
        IQueryable<TEntity> Find();

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="criteria">查询条件对象</param>
        /// <returns></returns>
        IQueryable<TEntity> Find(ICriteria<TEntity> criteria);

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate);

        #endregion

        #region Find(根据主键查找实体)

        /// <summary>
        /// 根据主键查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        TEntity Find(object id);

        /// <summary>
        /// 根据主键查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        Task<TEntity> FindAsync(object id);

        #endregion

        #region FindByIds(根据主键集合查找实体集合)

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        List<TEntity> FindByIds(params TKey[] ids);

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        List<TEntity> FindByIds(IEnumerable<TKey> ids);

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        List<TEntity> FindByIds(string ids);

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        Task<List<TEntity>> FindByIdsAsync(params TKey[] ids);

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids);

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        Task<List<TEntity>> FindByIdsAsync(string ids);

        #endregion

        #region Single(获取单个实体)

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        TEntity Single(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate);

        #endregion

        #region FindAll(查找实体集合)

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null);

        #endregion

        #region Exists(判断实体是否存在)

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        bool Exists(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        bool Exists(params TKey[] ids);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(params TKey[] ids);

        #endregion

        #region Count(获取实体个数)

        /// <summary>
        /// 获取实体个数
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        int Count(Expression<Func<TEntity, bool>> predicate = null);

        /// <summary>
        /// 获取实体个数
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null);

        #endregion

        #region Query(查询)

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        List<TEntity> Query(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        Task<List<TEntity>> QueryAsync(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        List<TEntity> QueryAsNoTracking(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        Task<List<TEntity>> QueryAsNoTrackingAsync(IQueryBase<TEntity> query);

        #endregion

        #region PagerQuery(分页查询)

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        PagerList<TEntity> PagerQuery(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        Task<PagerList<TEntity>> PagerQueryAsync(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        PagerList<TEntity> PagerQueryAsNoTracking(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync(IQueryBase<TEntity> query);

        #endregion

    }
}
