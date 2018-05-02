using System;
using System.Collections.Generic;
using System.Text;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;

namespace Bing.Datas.Stores.Operations
{
    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    public interface IPageQuery<TEntity,in TKey> where TEntity:class,IKey<TKey>
    {
        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        List<TEntity> Query(IQueryBase<TEntity> query);

        /// <summary>
        /// 查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        List<TEntity> QueryAsNoTracking(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        PagerList<TEntity> PagerQuery(IQueryBase<TEntity> query);

        /// <summary>
        /// 分页查询 - 返回未跟踪的实体
        /// </summary>
        /// <param name="query">查询对象</param>
        /// <returns></returns>
        PagerList<TEntity> PagerQueryAsNoTracking(IQueryBase<TEntity> query);
    }
}
