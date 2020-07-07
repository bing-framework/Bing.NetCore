using System.Linq;

namespace Bing.Judgments
{
    /// <summary>
    /// 可查询集合(<see cref="IQueryable{T}"/>) 判断
    /// </summary>
    public static class QueryableJudgment
    {
        /// <summary>
        /// 判断 一个可查询集合 是否至少包含指定数量的元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="queryable">可查询集合</param>
        /// <param name="count">数量</param>
        public static bool ContainsAtLeast<T>(IQueryable<T> queryable,int count)
        {
            if (queryable is null)
                return false;
            return (from t in queryable.Take(count) select t).Count() >= count;
        }

        /// <summary>
        /// 判断 两个可查询集合 是否包含相同数量的元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="queryable">可查询集合</param>
        /// <param name="targetQueryable">可查询集合</param>
        public static bool ContainsEqualCount<T>(IQueryable<T> queryable,IQueryable<T> targetQueryable)
        {
            if (queryable is null && targetQueryable is null)
                return true;
            if (queryable is null || targetQueryable is null)
                return false;
            return queryable.Count().Equals(targetQueryable.Count());
        }
    }
}
