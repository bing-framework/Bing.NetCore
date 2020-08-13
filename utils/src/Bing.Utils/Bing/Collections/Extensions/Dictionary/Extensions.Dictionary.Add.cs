using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Collections
{
    /// <summary>
    /// 字典(<see cref="IDictionary{TKey,TValue}"/>) 扩展
    /// </summary>
    public static partial class DictionaryExtensions
    {
        /// <summary>
        /// 添加。将其它字典添加到当前字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="other">其它字典</param>
        public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IDictionary<TKey, TValue> other)
        {
            foreach (var item in other)
                @this.Add(item.Key, item.Value);
        }

        /// <summary>
        /// 添加。将键值对集合添加到当前字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="values">键值对集合</param>
        /// <param name="replaceExisted">是否替换已存在的键值对</param>
        public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> @this, IEnumerable<KeyValuePair<TKey, TValue>> values, bool replaceExisted)
        {
            foreach (var item in values)
            {
                if (@this.ContainsKey(item.Key) && replaceExisted)
                {
                    @this[item.Key] = item.Value;
                    continue;
                }
                if (!@this.ContainsKey(item.Key))
                    @this.Add(item.Key, item.Value);
            }
        }

        /// <summary>
        /// 添加。如果不存在指定键，则添加到当前字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public static bool AddIfNotContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue value)
        {
            if (!@this.ContainsKey(key))
            {
                @this.Add(key, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 添加。如果不存在指定键，则添加到当前字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="valueFactory">值函数</param>
        public static bool AddIfNotContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TValue> valueFactory)
        {
            if (!@this.ContainsKey(key))
            {
                @this.Add(key, valueFactory());
                return true;
            }
            return false;
        }

        /// <summary>
        /// 添加。如果不存在指定键，则添加到当前字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="valueFactory">值函数</param>
        public static bool AddIfNotContainsKey<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> valueFactory)
        {
            if (!@this.ContainsKey(key))
            {
                @this.Add(key, valueFactory(key));
                return true;
            }
            return false;
        }

        /// <summary>
        /// 添加或更新
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue value)
        {
            if (!@this.ContainsKey(key))
                @this.Add(new KeyValuePair<TKey, TValue>(key, value));
            else
                @this[key] = value;
            return @this[key];
        }

        /// <summary>
        /// 添加或更新
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="addValue">添加值</param>
        /// <param name="updateValueFactory">更新值函数</param>
        public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (!@this.ContainsKey(key))
                @this.Add(new KeyValuePair<TKey, TValue>(key, addValue));
            else
                @this[key] = updateValueFactory(key, @this[key]);
            return @this[key];
        }

        /// <summary>
        /// 添加或更新
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="this">字典</param>
        /// <param name="key">键</param>
        /// <param name="addValueFactory">添加值函数</param>
        /// <param name="updateValueFactory">更新值函数</param>
        public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (!@this.ContainsKey(key))
                @this.Add(new KeyValuePair<TKey, TValue>(key, addValueFactory(key)));
            else
                @this[key] = updateValueFactory(key, @this[key]);
            return @this[key];
        }
    }
}
