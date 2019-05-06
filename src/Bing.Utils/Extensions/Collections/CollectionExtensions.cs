using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Utils.Helpers;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 集合(<see cref="ICollection{T}"/>) 扩展
    /// </summary>
    public static class CollectionExtensions
    {
        #region AddIfNotContains(添加项。如果未包含，则添加)

        /// <summary>
        /// 添加项。如果未包含，则添加
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">集合</param>
        /// <param name="item">项</param>
        public static bool AddIfNotContains<T>(this ICollection<T> source, T item)
        {
            Check.NotNull(source, nameof(source));

            if (source.Contains(item))
            {
                return false;
            }
            source.Add(item);
            return true;
        }

        /// <summary>
        /// 添加项集合。如果未包含，则添加
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">集合</param>
        /// <param name="items">项集合</param>
        public static IEnumerable<T> AddIfNotContains<T>(this ICollection<T> source, IEnumerable<T> items)
        {
            Check.NotNull(source, nameof(source));

            var addedItems = new List<T>();

            foreach (var item in items)
            {
                if (source.Contains(item))
                {
                    continue;
                }

                source.Add(item);
                addedItems.Add(item);
            }

            return addedItems;
        }

        /// <summary>
        /// 添加项。如果未包含
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">集合</param>
        /// <param name="predicate">条件</param>
        /// <param name="itemFactory">获取项函数</param>
        /// <returns></returns>
        public static bool AddIfNotContains<T>(this ICollection<T> source, Func<T, bool> predicate, Func<T> itemFactory)
        {
            Check.NotNull(source, nameof(source));
            Check.NotNull(predicate, nameof(predicate));
            Check.NotNull(itemFactory, nameof(itemFactory));

            if (source.Any(predicate))
            {
                return false;
            }

            source.Add(itemFactory());
            return true;
        }

        #endregion

        #region RemoveAll(移除项)

        /// <summary>
        /// 移除项。指定集合
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">集合</param>
        /// <param name="items">集合项</param>
        public static void RemoveAll<T>(this ICollection<T> source, IEnumerable<T> items)
        {
            Check.NotNull(source,nameof(source));
            Check.NotNull(items, nameof(items));

            foreach (var item in items)
            {
                source.Remove(item);
            }
        }

        /// <summary>
        /// 移除项。按条件移除
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="source">集合</param>
        /// <param name="predicate">条件</param>
        /// <returns></returns>
        public static IList<T> RemoveAll<T>(this ICollection<T> source, Func<T, bool> predicate)
        {
            Check.NotNull(source, nameof(source));
            Check.NotNull(predicate, nameof(predicate));
            
            var items = source.Where(predicate).ToList();
            foreach (var item in items)
            {
                source.Remove(item);
            }

            return items;
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
            if (values == null)
            {
                return;
            }
            foreach (var value in values)
            {
                collection.Add(value);
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
