using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications.Dtos;
using Bing.Datas.EntityFramework.Extensions;
using Bing.Datas.Queries;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;
using Bing.Domains.Sessions;
using Bing.Extensions.AutoMapper;
using Bing.Logs;
using Microsoft.EntityFrameworkCore;

namespace Bing.Applications
{
    /// <summary>
    /// 查询服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    public abstract class QueryServiceBase<TEntity, TDto, TQueryParameter> : QueryServiceBase<TEntity, TDto, TQueryParameter, Guid>
        where TEntity : class, IAggregateRoot<TEntity, Guid>
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 初始化一个<see cref="QueryServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="repository">仓储</param>
        protected QueryServiceBase(IRepository<TEntity> repository) : base(repository)
        {
        }
    }
    /// <summary>
    /// 查询服务
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TDto">数据传输对象类型</typeparam>
    /// <typeparam name="TQueryParameter">查询参数类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public abstract class QueryServiceBase<TEntity, TDto, TQueryParameter, TKey> : IQueryService<TDto, TQueryParameter>
        where TEntity : class, IAggregateRoot<TEntity, TKey>
        where TDto : IDto, new()
        where TQueryParameter : IQueryParameter
    {
        /// <summary>
        /// 仓储
        /// </summary>
        private readonly IRepository<TEntity, TKey> _repository;

        /// <summary>
        /// 日志组件
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }

        /// <summary>
        /// 查询时是否跟踪对象
        /// </summary>
        protected virtual bool IsTracking => false;

        /// <summary>
        /// 初始化一个<see cref="QueryServiceBase{TEntity,TDto,TQueryParameter,TKey}"/>类型的实例
        /// </summary>
        /// <param name="repository">仓储</param>
        protected QueryServiceBase(IRepository<TEntity, TKey> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            Log = Bing.Logs.Log.Null;
            Session = Bing.Domains.Sessions.Session.Null;
        }

        /// <summary>
        /// 转换为数据传输对象
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        protected virtual TDto ToDto(TEntity entity)
        {
            return entity.MapTo<TDto>();
        }

        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        public List<TDto> GetAll()
        {
            return _repository.FindAll().Select(ToDto).ToList();
        }

        /// <summary>
        /// 获取全部
        /// </summary>
        /// <returns></returns>
        public async Task<List<TDto>> GetAllAsync()
        {
            var entities = await _repository.FindAllAsync();
            return entities.Select(ToDto).ToList();
        }

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <returns></returns>
        public TDto GetById(object id)
        {
            var key = Bing.Utils.Helpers.Conv.To<TKey>(id);
            return ToDto(_repository.Find(key));
        }

        /// <summary>
        /// 通过编号获取
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <returns></returns>
        public async Task<TDto> GetByIdAsync(object id)
        {
            var key = Bing.Utils.Helpers.Conv.To<TKey>(id);
            return ToDto(await _repository.FindAsync(key));
        }

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表，范例："1,2"</param>
        /// <returns></returns>
        public List<TDto> GetByIds(string ids)
        {
            return _repository.FindByIds(ids).Select(ToDto).ToList();
        }

        /// <summary>
        /// 通过编号列表获取
        /// </summary>
        /// <param name="ids">用逗号分隔带额Id列表，范例："1,2"</param>
        /// <returns></returns>
        public async Task<List<TDto>> GetByIdsAsync(string ids)
        {
            var entities = await _repository.FindByIdsAsync(ids);
            return entities.Select(ToDto).ToList();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        public List<TDto> Query(TQueryParameter parameter)
        {
            var queryable = ExecuteQuery(parameter);
            return queryable.ToList().Select(ToDto).ToList();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        public async Task<List<TDto>> QueryAsync(TQueryParameter parameter)
        {
            var queryable = ExecuteQuery(parameter);
            var entities = await queryable.ToListAsync();
            return entities.Select(ToDto).ToList();
        }

        /// <summary>
        /// 执行查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        private IQueryable<TEntity> ExecuteQuery(TQueryParameter parameter)
        {
            var query = CreateQuery(parameter);
            var queryable = Filter(query);
            queryable = Filter(queryable);
            var order = query.GetOrder();
            return string.IsNullOrWhiteSpace(order) ? queryable : queryable.OrderBy(order);
        }

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        protected abstract IQueryBase<TEntity> CreateQuery(TQueryParameter parameter);

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="query">查询条件</param>
        /// <returns></returns>
        private IQueryable<TEntity> Filter(IQueryBase<TEntity> query)
        {
            return IsTracking ? _repository.Find().Where(query) : _repository.FindAsNoTracking().Where(query);
        }

        /// <summary>
        /// 过滤
        /// </summary>
        /// <param name="queryable">查询条件</param>
        /// <returns></returns>
        protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> queryable)
        {
            return queryable;
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        public PagerList<TDto> PagerQuery(TQueryParameter parameter)
        {
            var query = CreateQuery(parameter);
            var pager = query.GetPager();
            return ExecutePagerQuery(query, pager).ToPagerList(pager).Convert(ToDto);
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        /// <returns></returns>
        public async Task<PagerList<TDto>> PagerQueryAsync(TQueryParameter parameter)
        {
            var query = CreateQuery(parameter);
            var pager = query.GetPager();
            var queryable = ExecutePagerQuery(query, pager);
            var result = await queryable.ToPagerListAsync(pager);
            return result.Convert(ToDto);
        }

        /// <summary>
        /// 执行分页查询
        /// </summary>
        /// <param name="query">查询条件</param>
        /// <param name="pager">分页</param>
        /// <returns></returns>
        private IQueryable<TEntity> ExecutePagerQuery(IQueryBase<TEntity> query, IPager pager)
        {
            var queryable = Filter(query);
            queryable = Filter(queryable);
            var order = query.GetOrder();
            if (string.IsNullOrWhiteSpace(order))
            {
                order = "Id";
            }
            return queryable.OrderBy(order).Pager(pager);
        }
    }
}
