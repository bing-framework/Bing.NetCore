using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Domains.Entities;
using Bing.Validations.Aspects;

namespace Bing.Domains.Repositories
{
    /// <summary>
    /// 精简仓储 - 配合Po使用
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public interface ICompactRepository<TEntity> : ICompactRepository<TEntity, Guid>
        where TEntity : class, IAggregateRoot
    {
    }

    /// <summary>
    /// 精简仓储 - 配合Po使用
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">实体标识类型</typeparam>
    public interface ICompactRepository<TEntity, in TKey> where TEntity : class, IAggregateRoot
    {
        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        TEntity Find(object id);

        /// <summary>
        /// 查找实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        Task<TEntity> FindAsync(object id);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">实体标识列表</param>
        /// <returns></returns>
        List<TEntity> FindByIds(params TKey[] ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        List<TEntity> FindByIds(IEnumerable<TKey> ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">实体标识列表</param>
        /// <returns></returns>
        Task<List<TEntity>> FindByIdsAsync(params TKey[] ids);

        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        Task<List<TEntity>> FindByIdsAsync(IEnumerable<TKey> ids);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        bool Exists(params TKey[] ids);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        /// <param name="ids">实体标识集合</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(params TKey[] ids);

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
        /// <returns></returns>
        Task AddAsync([Valid] TEntity entity);

        /// <summary>
        /// 添加实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        /// <returns></returns>
        Task AddAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        void Update([Valid] TEntity entity);

        /// <summary>
        /// 修改实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        Task UpdateAsync([Valid] TEntity entity);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">实体标识</param>
        void Remove(TKey id);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="id">实体标识</param>
        /// <returns></returns>
        Task RemoveAsync(TKey id);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        void Remove(TEntity entity);

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns></returns>
        Task RemoveAsync(TEntity entity);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">实体编号集合</param>
        void Remove(IEnumerable<TKey> ids);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="ids">实体编号集合</param>
        /// <returns></returns>
        Task RemoveAsync(IEnumerable<TKey> ids);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        void Remove(IEnumerable<TEntity> entities);

        /// <summary>
        /// 移除实体集合
        /// </summary>
        /// <param name="entities">实体集合</param>
        void RemoveAsync(IEnumerable<TEntity> entities);
    }
}
