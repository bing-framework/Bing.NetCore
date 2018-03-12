using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Utils.Helpers;

namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 集合(ICollection)扩展
    /// </summary>
    public static class CollectionExtensions
    {
        #region AddIfNotExist(添加项。如果不存在，则添加)

        /// <summary>
        /// 添加项。如果不存在，则添加
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="value">值</param>
        public static void AddIfNotExist<T>(this ICollection<T> collection, T value)
        {
            collection.CheckNotNull(nameof(collection));
            if (!collection.Contains(value))
            {
                collection.Add(value);
            }
        }

        #endregion

        #region AddRangeIfNotExist(添加批量项。如果不存在，则添加)

        /// <summary>
        /// 添加批量项。如果不存在，则添加
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="values">值</param>
        public static void AddRangeIfNotExist<T>(this ICollection<T> collection, IEnumerable<T> values)
        {
            collection.CheckNotNull(nameof(collection));
            foreach (var value in values)
            {
                collection.AddIfNotExist(value);
            }
        }

        #endregion

        #region AddRange(添加批量项)

        /// <summary>
        /// 添加批量项。
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="values">值</param>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> values)
        {
            collection.CheckNotNull(nameof(collection));
            values.CheckNotNull(nameof(values));

            foreach (var value in values)
            {
                collection.Add(value);
            }
        }

        #endregion

        #region AddIfNotNull(添加项。如果不为空，则添加)

        /// <summary>
        /// 添加项。如果不为空，则添加
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="value">值</param>
        public static void AddIfNotNull<T>(this ICollection<T> collection, T value) where T : class
        {
            collection.CheckNotNull(nameof(collection));
            if (value != null)
            {
                collection.Add(value);
            }
        }

        #endregion

        #region RemoveRange(移除批量项)

        /// <summary>
        /// 移除批量项
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="values">值</param>
        public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> values)
        {
            collection.CheckNotNull(nameof(collection));
            values.CheckNotNull(nameof(values));

            foreach (var value in values)
            {
                collection.Remove(value);
            }
        }

        #endregion

        #region RemoveIf(移除项。如果符合条件，则移除)

        /// <summary>
        /// 移除项。如果符合条件，则移除
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="condition">移除条件</param>
        public static void RemoveIf<T>(this ICollection<T> collection, Func<T, bool> condition)
        {
            var itemsToRemove = collection.Where(condition).ToList();

            foreach (var item in itemsToRemove)
            {
                collection.Remove(item);
            }
        }

        #endregion

        #region Sort(排序)

        /// <summary>
        /// 排序
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="comparer">比较器</param>
        public static void Sort<T>(this ICollection<T> collection, IComparer<T> comparer = null)
        {
            comparer = comparer ?? Comparer<T>.Default;
            var list=new List<T>(collection);
            list.Sort(comparer);
            collection.ReplaceItems(list);
        }

        #endregion

        #region ReplaceItems(替换项)

        /// <summary>
        /// 替换项
        /// </summary>
        /// <typeparam name="TItem">项类型</typeparam>
        /// <typeparam name="TNewItem">新项类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="newItems">新项集合</param>
        /// <param name="createItemAction">创建项操作</param>
        public static void ReplaceItems<TItem, TNewItem>(this ICollection<TItem> collection,
            IEnumerable<TNewItem> newItems, Func<TNewItem, TItem> createItemAction)
        {
            collection.CheckNotNull(nameof(collection));
            newItems.CheckNotNull(nameof(newItems));
            createItemAction.CheckNotNull(nameof(createItemAction));

            collection.Clear();
            var convertedNewItems = newItems.Select(createItemAction);
            collection.AddRange(convertedNewItems);
        }

        /// <summary>
        /// 替换项
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="collection">集合</param>
        /// <param name="newItems">新项集合</param>
        public static void ReplaceItems<T>(this ICollection<T> collection, IEnumerable<T> newItems)
        {
            collection.CheckNotNull(nameof(collection));
            newItems.CheckNotNull(nameof(newItems));

            collection.ReplaceItems(newItems, x => x);
        }

        #endregion
    }
}
