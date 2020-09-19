using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Domain.Entities;
using Bing.Helpers;
using Bing.Mapping;

namespace Bing.Application.Services
{
    /// <summary>
    /// 查询应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public abstract class QueryAppServiceBase<TEntity, TDto, TQueryParameter> : QueryAppServiceBase<TEntity, TDto, TQueryParameter, Guid>
        where TEntity : class, IKey<Guid>, IVersion
        where TDto : new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="QueryAppServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="store">查询存储器</param>
        protected QueryAppServiceBase(IQueryStore<TEntity, Guid> store) : base(store)
        {
        }
    }

    /// <summary>
    /// 查询应用服务基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class QueryAppServiceBase<TEntity, TDto, TQueryParameter, TKey> : AppServiceBase, IQueryAppService<TDto, TQueryParameter>
        where TEntity : class, IKey<TKey>, IVersion
        where TDto : new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 查询存储器
        /// </summary>
        private readonly IQueryStore<TEntity, TKey> _store;

        /// <summary>
        /// 查询时是否跟踪对象
        /// </summary>
        protected virtual bool IsTracking => false;

        /// <summary>
        /// 初始化一个<see cref="QueryAppServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="store">查询存储器</param>
        protected QueryAppServiceBase(IQueryStore<TEntity, TKey> store) => _store = store ?? throw new ArgumentNullException(nameof(store));

        /// <summary>
        /// 转换为数据传输对象
        /// </summary>
        /// <param name="entity">实体</param>
        protected virtual TDto ToDto(TEntity entity) => entity.MapTo<TDto>();

        #region GetById(通过编号获取)

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        public virtual TDto GetById(object id)
        {
            var key = Conv.To<TKey>(id);
            return ToDto(_store.Find(key));
        }

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        public virtual async Task<TDto> GetByIdAsync(object id)
        {
            var key = Conv.To<TKey>(id);
            return ToDto(await _store.FindAsync(key));
        }

        #endregion

        #region GetByIds(通过编号列表获取)

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        public virtual List<TDto> GetByIds(string ids) => _store.FindByIds(ids).Select(ToDto).ToList();

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
        public virtual async Task<List<TDto>> GetByIdsAsync(string ids)
        {
            var entities = await _store.FindByIdsAsync(ids);
            return entities.Select(ToDto).ToList();
        }

        #endregion

        #region GetAll(获取全部)

        /// <summary>
        /// 获取全部
        /// </summary>
        public virtual List<TDto> GetAll() => _store.FindAll().Select(ToDto).ToList();

        /// <summary>
        /// 获取全部
        /// </summary>
        public virtual async Task<List<TDto>> GetAllAsync()
        {
            var entities = await _store.FindAllAsync();
            return entities.Select(ToDto).ToList();
        }

        #endregion

        #region Query(查询)

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public virtual List<TDto> Query(TQueryParameter parameter)
        {
            if (parameter == null)
                return new List<TDto>();
            return ExecuteQuery(parameter).ToList().Select(ToDto).ToList();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public virtual async Task<List<TDto>> QueryAsync(TQueryParameter parameter)
        {
            if (parameter == null)
                return new List<TDto>();
            return (await AsyncExecuter.ToListAsync(ExecuteQuery(parameter))).Select(ToDto).ToList();
        }

        /// <summary>
        /// 执行查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        private IQueryable<TEntity> ExecuteQuery(TQueryParameter parameter)
        {
            var query = CreateQuery(parameter);
            var queryable = Filter(query);
            queryable = Filter(queryable, parameter);
            var order = query.GetOrder();
            return string.IsNullOrWhiteSpace(order) ? queryable : queryable.OrderBy(order);
        }

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        protected virtual IQueryBase<TEntity> CreateQuery(TQueryParameter parameter) => new Query<TEntity>(parameter);

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="query">查询条件</param>
        private IQueryable<TEntity> Filter(IQueryBase<TEntity> query) => IsTracking ? _store.Find().Where(query) : _store.FindAsNoTracking().Where(query);

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="queryable">查询条件</param>
        /// <param name="parameter">查询参数</param>
        protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> queryable, TQueryParameter parameter) => queryable;

        #endregion

        #region PagerQuery(分页查询)

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public virtual PagerList<TDto> PagerQuery(TQueryParameter parameter)
        {
            if (parameter == null)
                return new PagerList<TDto>();
            var query = CreateQuery(parameter);
            var queryable = Filter(query);
            queryable = Filter(queryable, parameter);
            return queryable.ToPagerList(query.GetPager()).Convert(ToDto);
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        public virtual async Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter)
        {
            if (parameter == null)
                return new PagerList<TDto>();
            var query = CreateQuery(parameter);
            var queryable = Filter(query);
            queryable = Filter(queryable, parameter);
            var pager = query.GetPager();
            Bing.Data.Queries.Internal.Helper.InitOrder(queryable, pager);
            if (pager.TotalCount <= 0)
                pager.TotalCount = await AsyncExecuter.CountAsync(queryable);
            var orderedQueryable = Bing.Data.Queries.Internal.Helper.GetOrderedQueryable(queryable, pager);
            if (orderedQueryable == null)
                throw new ArgumentException("必须设置排序字段");
            queryable = orderedQueryable.Skip(pager.GetSkipCount()).Take(pager.PageSize);
            var pagerList = new PagerList<TEntity>(pager, await AsyncExecuter.ToListAsync(queryable));
            return pagerList.Convert(ToDto);
        }

        #endregion
    }
}
