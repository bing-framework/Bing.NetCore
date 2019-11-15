using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Domains.Entities;

namespace Bing.Datas.Stores.Operations
{
    /// <summary>
    /// 查找实体列表
    /// </summary>
    /// <typeparam name="TEntity">对象类型</typeparam>
    /// <typeparam name="TKey">对象标识类型</typeparam>
    public interface IFindAll<TEntity, in TKey> where TEntity : class, IKey<TKey>
    {
        /// <summary>
        /// 查找实体列表
        /// </summary>
        /// <param name="predicate">查询条件</param>
        List<TEntity> FindAll(Expression<Func<TEntity, bool>> predicate = null);
    }
}
