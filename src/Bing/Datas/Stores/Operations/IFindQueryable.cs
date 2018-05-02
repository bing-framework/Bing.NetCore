using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Bing.Domains.Entities;
using Bing.Domains.Repositories;

namespace Bing.Datas.Stores.Operations
{
    /// <summary>
    /// 获取查询对象
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    public interface IFindQueryable<TEntity,in TKey> where TEntity:class,IKey<TKey>
    {
        /// <summary>
        /// 获取未跟踪查询对象
        /// </summary>
        /// <returns></returns>
        IQueryable<TEntity> FindAsNoTracking();

        /// <summary>
        /// 获取查询对象
        /// </summary>
        /// <returns></returns>
        IQueryable<TEntity> Find();

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="criteria">查询条件</param>
        /// <returns></returns>
        IQueryable<TEntity> Find(ICriteria<TEntity> criteria);

        /// <summary>
        /// 查找
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns></returns>
        IQueryable<TEntity> Find(Expression<Func<TEntity, bool>> predicate);
    }
}
