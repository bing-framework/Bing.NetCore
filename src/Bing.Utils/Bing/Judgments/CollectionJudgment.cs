using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Judgments
{
    /// <summary>
    /// 集合判断
    /// </summary>
    public static class CollectionJudgment
    {
        /// <summary>
        /// 判断 <see cref="IEnumerable"/> 是否为 null
        /// </summary>
        /// <param name="enumerable">集合</param>
        public static bool IsNull(IEnumerable enumerable) => enumerable is null;

        /// <summary>
        /// 判断 <see cref="IEnumerable"/> 是否为空、null
        /// </summary>
        /// <param name="enumerable">集合</param>
        public static bool IsNullOrEmpty(IEnumerable enumerable)
        {
            if (enumerable is null)
                return true;
            return !enumerable.Cast<object>().Any();
        }

        /// <summary>
        /// 判断 <see cref="IEnumerable{T}"/> 是否为空、null
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="enumerable">集合</param>
        public static bool IsNullOrEmpty<T>(IEnumerable<T> enumerable) => enumerable is null || !enumerable.Any();

        /// <summary>
        /// 判断 一个集合 是否至少包含指定数量的元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="count">数量</param>
        public static bool ContainsAtLeast<T>(ICollection<T> collection, int count) => collection?.Count >= count;

        /// <summary>
        /// 判断 两个集合 是否包含相同数量的元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="leftCollection">集合</param>
        /// <param name="rightCollection">集合</param>
        public static bool ContainsEqualCount<T>(ICollection<T> leftCollection, ICollection<T> rightCollection)
        {
            if (leftCollection is null && rightCollection is null)
                return true;
            if (leftCollection is null || rightCollection is null)
                return false;
            return leftCollection.Count.Equals(rightCollection.Count);
        }
    }
}
