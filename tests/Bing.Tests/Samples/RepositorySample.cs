using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;

namespace Bing.Tests.Samples
{
    /// <summary>
    /// 实体样例
    /// </summary>
    [Display(Name = "实体样例")]
    public class EntitySample : AggregateRoot<EntitySample>
    {
        /// <summary>
        /// 初始化一个<see cref="EntitySample"/>类型的实例
        /// </summary>
        public EntitySample() : this(Guid.NewGuid()) { }

        /// <summary>
        /// 初始化一个<see cref="EntitySample"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        public EntitySample(Guid id) : base(id) { }

        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 忽略值
        /// </summary>
        [IgnoreMap]
        public string IgnoreValue { get; set; }
    }

    /// <summary>
    /// 仓储样例
    /// </summary>
    public interface IRepositorySample : IRepository<EntitySample> { }

    /// <summary>
    /// 仓储样例
    /// </summary>
    public class RepositorySample : IRepositorySample
    {
        /// <summary>
        /// 获取工作单元
        /// </summary>
        public IUnitOfWork GetUnitOfWork()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取未跟踪查询对象
        /// </summary>
        public IQueryable<EntitySample> FindAsNoTracking()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取查询对象
        /// </summary>
        public IQueryable<EntitySample> Find()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="criteria">查询条件</param>
        public IQueryable<EntitySample> Find(ICriteria<EntitySample> criteria)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public IQueryable<EntitySample> Find(Expression<Func<EntitySample, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">标识</param>
        public EntitySample Find(object id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<EntitySample> FindAsync(object id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        public List<EntitySample> FindByIds(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        public List<EntitySample> FindByIds(IEnumerable<Guid> ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        public List<EntitySample> FindByIds(string ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        public async Task<List<EntitySample>> FindByIdsAsync(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<List<EntitySample>> FindByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        public async Task<List<EntitySample>> FindByIdsAsync(string ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找未跟踪单个实体
        /// </summary>
        /// <param name="id">标识</param>
        public EntitySample FindByIdNoTracking(Guid id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找未跟踪单个实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<EntitySample> FindByIdNoTrackingAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        public List<EntitySample> FindByIdsNoTracking(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        public List<EntitySample> FindByIdsNoTracking(IEnumerable<Guid> ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        public List<EntitySample> FindByIdsNoTracking(string ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        public async Task<List<EntitySample>> FindByIdsNoTrackingAsync(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">标识列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<List<EntitySample>> FindByIdsNoTrackingAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="ids">逗号分隔的标识列表，范例："1,2"</param>
        public async Task<List<EntitySample>> FindByIdsNoTrackingAsync(string ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public EntitySample Single(Expression<Func<EntitySample, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找单个实体
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<EntitySample> SingleAsync(Expression<Func<EntitySample, bool>> predicate, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public List<EntitySample> FindAll(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public async Task<List<EntitySample>> FindAllAsync(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public List<EntitySample> FindAllNoTracking(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找实体列表，不跟踪
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public async Task<List<EntitySample>> FindAllNoTrackingAsync(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="ids">标识列表</param>
        public bool Exists(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="ids">标识列表</param>
        public async Task<bool> ExistsAsync(params Guid[] ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public bool Exists(Expression<Func<EntitySample, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public async Task<bool> ExistsAsync(Expression<Func<EntitySample, bool>> predicate)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public int Count(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查找数量
        /// </summary>
        /// <param name="predicate">查询条件</param>
        public async Task<int> CountAsync(Expression<Func<EntitySample, bool>> predicate = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        public List<EntitySample> Query(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        public List<EntitySample> QueryAsNoTracking(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        public PagerList<EntitySample> PagerQuery(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        public PagerList<EntitySample> PagerQueryAsNoTracking(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        public async Task<List<EntitySample>> QueryAsync(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        public async Task<List<EntitySample>> QueryAsNoTrackingAsync(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        public async Task<PagerList<EntitySample>> PagerQueryAsync(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        public async Task<PagerList<EntitySample>> PagerQueryAsNoTrackingAsync(IQueryBase<EntitySample> query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Add(EntitySample entity)
        {
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public void Add(IEnumerable<EntitySample> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task AddAsync(EntitySample entity, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task AddAsync(IEnumerable<EntitySample> entities, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Update(EntitySample entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public void Update(IEnumerable<EntitySample> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        public async Task UpdateAsync(EntitySample entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 修改实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public async Task UpdateAsync(IEnumerable<EntitySample> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        public void Remove(object id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        public void Remove(EntitySample entity)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        public void Remove(IEnumerable<Guid> ids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        public void Remove(IEnumerable<EntitySample> entities)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task RemoveAsync(object id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task RemoveAsync(EntitySample entity, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">标识集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task RemoveAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task RemoveAsync(IEnumerable<EntitySample> entities, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
