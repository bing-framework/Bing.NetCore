using System;
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
        /// 获取或添加。如果指定键的值不存在，则添加值并返回
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue value)
        {
            if (!@this.ContainsKey(key))
                @this.Add(key, value);
            return @this[key];
        }

        /// <summary>
        /// 获取或添加。如果指定键的值不存在，则添加值并返回
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="valueFactory">值函数</param>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TValue> valueFactory)
        {
            if (!@this.ContainsKey(key))
                @this.Add(key, valueFactory());
            return @this[key];
        }

        /// <summary>
        /// 获取或添加。如果指定键的值不存在，则添加值并返回
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="valueFactory">值函数</param>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (!@this.ContainsKey(key))
                @this.Add(key, valueFactory(key));
            return @this[key];
        }

        /// <summary>
        /// 获取键。根据值反向查找键
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="value">值</param>
        public static TKey GetKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, TValue value)
        {
            foreach (var item in @this.Where(x => x.Value.Equals(value)))
                return item.Key;
            return default;
        }

        /// <summary>
        /// 获取值。根据指定键获取值，如果未找到，则返回默认值
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue defaultValue = default) =>
            @this.TryGetValue(key, out var value) ? value : defaultValue;
    }
}
