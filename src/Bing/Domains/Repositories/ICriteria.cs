using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Bing.Domains.Repositories
{
    /// <summary>
    /// 查询条件
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public interface ICriteria<TEntity>
    {
        /// <summary>
        /// 获取查询条件
        /// </summary>
        /// <returns></returns>
        Expression<Func<TEntity, bool>> GetPredicate();
    }
}
