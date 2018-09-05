using System;
using System.Linq;
using System.Linq.Expressions;
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
        /// 转换为分页列表，包含排序分页操作
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="query">数据源</param>
        /// <param name="pager">分页对象</param>
        /// <returns></returns>
        public static async Task<PagerList<TEntity>> ToPagerListAsync<TEntity>(this IQueryable<TEntity> query,
            IPager pager)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }
            if (pager == null)
            {
                throw new ArgumentNullException(nameof(pager));
            }
            return new PagerList<TEntity>(pager,await query.Page(pager).ToListAsync());
        }

        #endregion

        #region Between(范围查询)

        /// <summary>
        /// 范围查询
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <param name="queryable">查询源</param>
        /// <param name="keySelector">键选择器</param>
        /// <param name="begin">开始范围</param>
        /// <param name="end">结束范围</param>
        /// <returns></returns>
        public static IQueryable<TEntity> Between<TEntity, TKey>(this IQueryable<TEntity> queryable,
            Expression<Func<TEntity, TKey>> keySelector, TKey begin, TKey end) where TKey : IComparable<TKey>
        {
            Expression key = Expression.Invoke(keySelector, keySelector.Parameters.ToArray());
            Expression beginBound=Expression.GreaterThanOrEqual(key,Expression.Constant(begin));
            Expression endBound = Expression.LessThanOrEqual(key, Expression.Constant(end));
            Expression and = Expression.AndAlso(beginBound, endBound);
            Expression<Func<TEntity, bool>> lambda = Expression.Lambda<Func<TEntity, bool>>(and, keySelector.Parameters);
            return queryable.Where(lambda.Compile()).AsQueryable();
        }

        #endregion
    }
}
