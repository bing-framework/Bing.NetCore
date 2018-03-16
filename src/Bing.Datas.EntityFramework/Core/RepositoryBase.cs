using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Bing.Datas.EntityFramework.Internal;
using Bing.Datas.Queries;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class RepositoryBase<TEntity> : RepositoryBase<TEntity, Guid>, IRepository<TEntity>
        where TEntity : class, IAggregateRoot<TEntity, Guid>
    {
        /// <summary>
        /// 初始化一个<see cref="RepositoryBase{TEntity}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected RepositoryBase(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }

    /// <summary>
    /// 仓储
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : class, IAggregateRoot<TEntity, TKey>
    {
        #region 字段

        /// <summary>
        /// 数据上下文包装器
        /// </summary>
        private readonly DbContextWrapper<TEntity, TKey> _wrapper;

        #endregion

        #region 属性

        /// <summary>
        /// 工作单元
        /// </summary>
        protected UnitOfWorkBase UnitOfWork { get; }

        /// <summary>
        /// 实体集
        /// </summary>
        protected DbSet<TEntity> Set { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="RepositoryBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        protected RepositoryBase(IUnitOfWork unitOfWork)
        {
            _wrapper = new DbContextWrapper<TEntity, TKey>(unitOfWork);
            UnitOfWork = _wrapper.UnitOfWork;
            Set = _wrapper.Set;
        }

        #endregion

        #region Find(懒加载查找实体集合)

        /// <summary>
        /// 获取未跟踪的实体集
        /// </summary>
        /// <returns></returns>
        public IQueryable<TEntity> FindAsNoTracking()
        {
            return Set.AsNoTracking();
        }

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <returns></returns>
        public IQueryable<TEntity> Find()
        {
            return _wrapper.Find();
        }

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="criteria">查询条件对象</param>
        /// <returns></returns>
        public IQueryable<TEntity> Find(ICriteria<TEntity> criteria)
        {
            return Find().Where(criteria);
        }

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate)
        {
            return Find().Where(predicate);
        }

        #endregion

        #region Find(根据主键查找实体)

        /// <summary>
        /// 根据主键查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        public TEntity Find(object id)
        {
            return _wrapper.Find(id);
        }

        /// <summary>
        /// 根据主键查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        public async Task<TEntity> FindAsync(object id)
        {
            return await _wrapper.FindAsync(id);
        }

        #endregion

        #region FindByIds(根据主键集合查找实体集合)

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public List<TEntity> FindByIds(params TKey[] ids)
        {
            return _wrapper.FindByIds(ids);
        }

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public List<TEntity> FindByIds(IEnumerable<TKey> ids)
        {
            return _wrapper.FindByIds(ids);
        }

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        public List<TEntity> FindByIds(string ids)
        {
            var idList = Bing.Utils.Helpers.Conv.ToList<TKey>(ids);
            return FindByIds(idList);
        }

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public async Task<List<TEntity>> FindByIdsAsync(params TKey[] ids)
        {
            return await _wrapper.FindByIdsAsync(ids);
        }

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public async Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids)
        {
            return await _wrapper.FindByIdsAsync(ids);
        }

        /// <summary>
        /// 根据主键集合查找实体集合
        /// </summary>
        /// <param name="ids">逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        public async Task<List<TEntity>> FindByIdsAsync(string ids)
        {
            var idList = Bing.Utils.Helpers.Conv.ToList<TKey>(ids);
            return await FindByIdsAsync(idList);
        }

        #endregion

        #region Single(获取单个实体)

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public TEntity Single(Expression<Func<TEntity, bool>> predicate)
        {
            return _wrapper.Single(predicate);
        }

        /// <summary>
        /// 获取单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public async Task<TEntity> SingleAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _wrapper.SingleAsync(predicate);
        }

        #endregion

        #region FindAll(查找实体集合)

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null)
        {
            return predicate == null ? Find().ToList() : Find(predicate).ToList();
        }

        /// <summary>
        /// 查找实体集合
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public async Task<List<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> predicate = null)
        {
            return predicate == null ? await Find().ToListAsync() : await Find(predicate).ToListAsync();
        }

        #endregion

        #region Exists(判断实体是否存在)

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public bool Exists(Expression<Func<TEntity, bool>> predicate)
        {
            return predicate != null && Find().Any(predicate);
        }

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public bool Exists(params TKey[] ids)
        {
            return ids != null && Exists(t => ids.Contains(t.Id));
        }

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return predicate != null && await Find().AnyAsync(predicate);
        }

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(params TKey[] ids)
        {
            return ids != null && await ExistsAsync(t => ids.Contains(t.Id));
        }

        #endregion

        #region Count(获取实体个数)

        /// <summary>
        /// 获取实体个数
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public int Count(Expression<Func<TEntity, bool>> predicate = null)
        {
            return predicate == null ? Set.Count() : Set.Count(predicate);
        }

        /// <summary>
        /// 获取实体个数
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null)
        {
            return predicate == null ? Set.CountAsync() : Set.CountAsync(predicate);
        }

        #endregion

        #region Query(查询)

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public List<TEntity> Query(IQueryBase<TEntity> query)
        {
            return _wrapper.Query(query);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public async Task<List<TEntity>> QueryAsync(IQueryBase<TEntity> query)
        {
            return await _wrapper.QueryAsync(query);
        }

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public List<TEntity> QueryAsNoTracking(IQueryBase<TEntity> query)
        {
            return _wrapper.QueryAsNoTracking(query);
        }

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public async Task<List<TEntity>> QueryAsNoTrackingAsync(IQueryBase<TEntity> query)
        {
            return await _wrapper.QueryAsNoTrackingAsync(query);
        }

        #endregion

        #region PagerQuery(分页查询)

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public PagerList<TEntity> PagerQuery(IQueryBase<TEntity> query)
        {
            return _wrapper.PagerQuery(query);
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public async Task<PagerList<TEntity>> PagerQueryAsync(IQueryBase<TEntity> query)
        {
            return await _wrapper.PagerQueryAsync(query);
        }

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public PagerList<TEntity> PagerQueryAsNoTracking(IQueryBase<TEntity> query)
        {
            return _wrapper.PagerQueryAsNoTracking(query);
        }

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        public async Task<PagerList<TEntity>> PagerQueryAsNoTrackingAsync(IQueryBase<TEntity> query)
        {
            return await _wrapper.PagerQueryAsNoTrackingAsync(query);
        }

        #endregion

        #region Add(添加实体)

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Add(TEntity entity)
        {
            _wrapper.Add(entity);
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public void Add(IEnumerable<TEntity> entities)
        {
            _wrapper.Add(entities);
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public async Task AddAsync(TEntity entity)
        {
            await _wrapper.AddAsync(entity);
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <returns></returns>
        public async Task AddAsync(IEnumerable<TEntity> entities)
        {
            await _wrapper.AddAsync(entities);
        }

        #endregion

        #region Update(修改实体)

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Update(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            var oldEntity = Find(entity.Id);
            _wrapper.Update(entity, oldEntity);
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public async Task UpdateAsync(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }
            var oldEntity = await _wrapper.FindAsync(entity.Id);
            _wrapper.Update(entity, oldEntity);
        }

        #endregion

        #region Remove(移除实体)

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">实体标识</param>
        public void Remove(TKey id)
        {
            _wrapper.Remove(id);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        public async Task RemoveAsync(TKey id)
        {
            await _wrapper.RemoveAsync(id);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Remove(TEntity entity)
        {
            _wrapper.Remove(entity);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        public async Task RemoveAsync(TEntity entity)
        {
            await _wrapper.RemoveAsync(entity);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">实体编号集合</param>
        public void Remove(IEnumerable<TKey> ids)
        {
            _wrapper.Remove(ids);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">实体编号集合</param>
        /// <returns></returns>
        public async Task RemoveAsync(IEnumerable<TKey> ids)
        {
            await _wrapper.RemoveAsync(ids);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public void Remove(IEnumerable<TEntity> entities)
        {
            _wrapper.Remove(entities);
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public async Task RemoveAsync(IEnumerable<TEntity> entities)
        {
            await _wrapper.RemoveAsync(entities);
        }

        #endregion

        #region GetUnitOfWork(获取工作单元)

        /// <summary>
        /// 获取工作单元
        /// </summary>
        /// <returns></returns>
        public IUnitOfWork GetUnitOfWork()
        {
            return UnitOfWork;
        }

        #endregion
    }
}
