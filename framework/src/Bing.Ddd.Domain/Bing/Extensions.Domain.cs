using System;
using System.Collections.Generic;
using Bing.Domain.Entities;

// ReSharper disable once CheckNamespace
namespace Bing
{
    /// <summary>
    /// 领域实体扩展
    /// </summary>
    public static partial class DomainExtensions
    {
        /// <summary>
        /// 比较
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="newList">新实体集合</param>
        /// <param name="oldList">旧实体集合</param>
        public static ListCompareResult<TEntity, Guid> Compare<TEntity>(this IEnumerable<TEntity> newList, IEnumerable<TEntity> oldList)
            where TEntity : IKey<Guid> =>
            Compare<TEntity, Guid>(newList, oldList);

        /// <summary>
        /// 比较
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <typeparam name="TKey">标识类型</typeparam>
        /// <param name="newList">新实体集合</param>
        /// <param name="oldList">旧实体集合</param>
        public static ListCompareResult<TEntity, TKey> Compare<TEntity, TKey>(this IEnumerable<TEntity> newList, IEnumerable<TEntity> oldList)
            where TEntity : IKey<TKey>
        {
            var comparator = new ListComparator<TEntity, TKey>();
            return comparator.Compare(newList, oldList);
        }

        /// <summary>
        /// 比较
        /// </summary>
        /// <param name="newList">新实体标识集合</param>
        /// <param name="oldList">旧实体标识集合</param>
        public static KeyListCompareResult<Guid> Compare(this IEnumerable<Guid> newList, IEnumerable<Guid> oldList) =>
            CompareKey(newList, oldList);

        /// <summary>
        /// 比较
        /// </summary>
        /// <param name="newList">新实体标识集合</param>
        /// <param name="oldList">旧实体标识集合</param>
        public static KeyListCompareResult<string> Compare(this IEnumerable<string> newList, IEnumerable<string> oldList) =>
            CompareKey(newList, oldList);

        /// <summary>
        /// 比较
        /// </summary>
        /// <param name="newList">新实体标识集合</param>
        /// <param name="oldList">旧实体标识集合</param>
        public static KeyListCompareResult<int> Compare(this IEnumerable<int> newList, IEnumerable<int> oldList) =>
            CompareKey(newList, oldList);

        /// <summary>
        /// 比较
        /// </summary>
        /// <param name="newList">新实体标识集合</param>
        /// <param name="oldList">旧实体标识集合</param>
        public static KeyListCompareResult<long> Compare(this IEnumerable<long> newList, IEnumerable<long> oldList) =>
            CompareKey(newList, oldList);

        /// <summary>
        /// 比较键
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <param name="newList">新实体标识集合</param>
        /// <param name="oldList">旧实体标识集合</param>
        private static KeyListCompareResult<TKey> CompareKey<TKey>(IEnumerable<TKey> newList, IEnumerable<TKey> oldList)
        {
            var comparator = new KeyListComparator<TKey>();
            return comparator.Compare(newList, oldList);
        }
    }
}
