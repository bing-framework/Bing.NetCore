using System.Linq;
using Bing.Helpers;

namespace System.Collections.Generic
{
    /// <summary>
    /// 集合(<see cref="ICollection"/>) 扩展
    /// </summary>
    public static partial class CollectionExtensions
    {
        #region IsNullOrEmpty(是否为空)

        /// <summary>
        /// 是否为空。判断 <see cref="ICollection{T}"/> 是否为空或空元素
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="source">源</param>
        public static bool IsNullOrEmpty<T>(this ICollection<T> source) => source == null || source.Count <= 0;

        #endregion

        #region AddIfNotContains(添加项)

        /// <summary>
        /// 添加项。如果指定项不再集合中，则将其添加到集合中
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="source">源</param>
        /// <param name="item">项</param>
        public static bool AddIfNotContains<T>(this ICollection<T> source, T item)
        {
            Check.NotNull(source, nameof(source));
            if (source.Contains(item))
                return false;
            source.Add(item);
            return true;
        }

        /// <summary>
        /// 添加项集合。将尚未添加到集合中的项添加到集合中
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="source">源</param>
        /// <param name="items">项集合</param>
        public static IEnumerable<T> AddIfNotContains<T>(this ICollection<T> source, IEnumerable<T> items)
        {
            Check.NotNull(source, nameof(source));
            var addedItems = new List<T>();
            foreach (var item in items)
            {
                if (source.Contains(item))
                    continue;
                source.Add(item);
                addedItems.Add(item);
            }
            return addedItems;
        }

        /// <summary>
        /// 添加项。根据给定的条件，将一个项添加到该集合中
        /// </summary>
        /// <typeparam name="T">泛型类型</typeparam>
        /// <param name="source">源</param>
        /// <param name="predicate">条件</param>
        /// <param name="itemFactory">项函数</param>
        public static bool AddIfNotContains<T>(this ICollection<T> source, Func<T, bool> predicate, Func<T> itemFactory)
        {
            Check.NotNull(source, nameof(source));
            Check.NotNull(predicate, nameof(predicate));
            Check.NotNull(itemFactory, nameof(itemFactory));

            if (source.Any(predicate))
                return false;
            source.Add(itemFactory());
            return true;
        }

        #endregion

        #region RemoveAll(移除所有项)

        ///// <summary>
        ///// 移除所有项。从集合中删除所有满足给定条件的项
        ///// </summary>
        ///// <typeparam name="T">泛型类型</typeparam>
        ///// <param name="source">源</param>
        ///// <param name="predicate">条件</param>
        //public static IList<T> RemoveAll<T>(this ICollection<T> source, Func<T, bool> predicate)
        //{
        //    var items = source.Where(predicate).ToList();
        //    foreach (var item in items)
        //        source.Remove(item);
        //    return items;
        //}

        #endregion
    }
}
