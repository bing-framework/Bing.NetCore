using System;
using System.Linq;
using System.Linq.Expressions;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// <see cref="IQueryable{T}"/> 扩展
    /// </summary>
    public static class QueryableExtensions
    {
        #region WhereIf(是否执行指定条件的查询)

        /// <summary>
        /// 是否执行指定条件的查询，根据第三方条件是否为真来决定
        /// </summary>
        /// <typeparam name="T">动态类型</typeparam>
        /// <param name="source">要查询的源</param>
        /// <param name="predicate">查询条件</param>
        /// <param name="condition">第三方条件</param>
        /// <returns>查询结果</returns>
        public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, Expression<Func<T, bool>> predicate,
            bool condition)
        {
            source.CheckNotNull(nameof(source));
            predicate.CheckNotNull(nameof(predicate));

            return condition ? source.Where(predicate) : source;
        }

        #endregion

    }
}
