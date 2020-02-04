using System;
using System.Linq;
using System.Linq.Expressions;
using Bing.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> 扩展
    /// </summary>
    public static partial class QueryableExtensions
    {
        #region WhereIf(是否执行指定条件的查询)

        /// <summary>
        /// 是否执行指定条件的查询，根据第三方条件是否为真来决定
        /// </summary>
        /// <typeparam name="T">动态类型</typeparam>
        /// <param name="source">要查询的源</param>
        /// <param name="predicate">查询条件</param>
        /// <param name="condition">第三方条件</param>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate,
            bool condition)
        {
            source.CheckNotNull(nameof(source));
            predicate.CheckNotNull(nameof(predicate));

            return condition ? source.Where(predicate) : source;
        }

        #endregion

        #region PageBy(分页)

        /// <summary>
        /// 分页
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="skipCount">跳过的行数</param>
        /// <param name="pageSize">每页记录数</param>
        public static IQueryable<T> PageBy<T>(this IQueryable<T> queryable, int skipCount, int pageSize)
        {
            Check.NotNull(queryable, nameof(queryable));

            return queryable.Skip(skipCount).Take(pageSize);
        }

        /// <summary>
        /// 分页
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <typeparam name="TQueryable">查询源类型</typeparam>
        /// <param name="queryable">数据源</param>
        /// <param name="skipCount">跳过的行数</param>
        /// <param name="pageSize">每页记录数</param>
        public static TQueryable PageBy<T, TQueryable>(this IQueryable<T> queryable, int skipCount, int pageSize)
            where TQueryable : IQueryable
        {
            Check.NotNull(queryable, nameof(queryable));

            return (TQueryable)queryable.Skip(skipCount).Take(pageSize);
        }

        #endregion

    }
}
