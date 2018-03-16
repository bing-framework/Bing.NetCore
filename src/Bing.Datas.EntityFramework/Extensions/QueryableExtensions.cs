using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Bing.Domains.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Extensions
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> 扩展
    /// </summary>
    public static partial class QueryableExtensions
    {
        #region ToPagerListAsync(转换为分页列表)

        /// <summary>
        /// 转换为分页列表
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="source">数据源</param>
        /// <param name="pager">分页对象</param>
        /// <returns></returns>
        public static async Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> source,
            IPager pager)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (pager == null)
            {
                throw new ArgumentNullException(nameof(pager));
            }
            var result = new PagerList<TEntity>(pager);
            result.AddRange(await source.ToListAsync());
            return result;
        }

        #endregion

        #region WhereIf(条件判断语句)

        /// <summary>
        /// Where If 条件语句，如果判断条件为True，则通过条件表达式进行过滤
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="predicate">条件表达式</param>
        /// <param name="condition">判断条件</param>
        /// <returns></returns>
        public static IQueryable<TEntity> WhereIf<TEntity>(this IQueryable<TEntity> query,
            Expression<Func<TEntity, bool>> predicate, bool condition)
        {
            return condition ? query.Where(predicate) : query;
        }

        #endregion

    }
}
