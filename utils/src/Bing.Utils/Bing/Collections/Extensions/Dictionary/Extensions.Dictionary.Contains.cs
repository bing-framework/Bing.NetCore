using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 字典(<see cref="IDictionary{TKey,TValue}"/>) 扩展
    /// </summary>
    public static partial class DictionaryExtensions
    {
        /// <summary>
        /// 是否包含任意键
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="keys">键数组</param>
        public static bool ContainsAnyKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, params TKey[] keys) => keys.Any(@this.ContainsKey);

        /// <summary>
        /// 是否包含所有键
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="keys">键数组</param>
        public static bool ContainsAllKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, params TKey[] keys) => keys.All(@this.ContainsKey);
    }
}
