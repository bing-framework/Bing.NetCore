using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字典扩展
    /// </summary>
    public static class DictionaryExtensions
    {
        #region GetOrDefault(获取指定Key对应的Value，若未找到则返回默认值)

        /// <summary>
        /// 获取指定Key对应的Value，若未找到则返回默认值
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns></returns>
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key,
            TValue defaultValue = default(TValue))
        {
            return dictionary.TryGetValue(key, out var obj) ? obj : defaultValue;
        }

        #endregion

        #region AddRange(批量添加键值对到字典)
        /// <summary>
        /// 批量添加键值对到字典中
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dict">字典</param>
        /// <param name="values">键值对集合</param>
        /// <param name="replaceExisted">是否替换已存在的键值对</param>
        /// <returns></returns>
        public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dict,
            IEnumerable<KeyValuePair<TKey, TValue>> values, bool replaceExisted)
        {
            foreach (var item in values)
            {
                if (dict.ContainsKey(item.Key) && replaceExisted)
                {
                    dict[item.Key] = item.Value;
                    continue;
                }
                if (!dict.ContainsKey(item.Key))
                {
                    dict.Add(item.Key, item.Value);
                }
            }
            return dict;
        }
        #endregion

        #region GetOrAdd(获取指定键的值，不存在则按指定委托添加值)

        /// <summary>
        /// 获取指定键的值，不存在则按指定委托添加值
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dict">字典</param>
        /// <param name="key">键</param>
        /// <param name="setValue">添加值的委托</param>
        /// <returns></returns>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key,
            Func<TKey, TValue> setValue)
        {
            if (!dict.TryGetValue(key, out var value))
            {
                value = setValue(key);
                dict.Add(key, value);
            }
            return value;
        }

        /// <summary>
        /// 获取指定键的值，不存在则按指定委托添加值
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="key">键</param>
        /// <param name="addFunc">添加值的委托</param>
        /// <returns></returns>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key,
            Func<TValue> addFunc)
        {
            if (dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }

            return dictionary[key] = addFunc();
        }

        #endregion

        #region Sort(字段排序)

        /// <summary>
        /// 对指定的字典进行排序
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns>排序后的字典</returns>
        public static IDictionary<TKey, TValue> Sort<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            return new SortedDictionary<TKey, TValue>(dictionary);
        }

        /// <summary>
        /// 对指定的字典进行排序
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="comparer">比较器，用于排序字典</param>
        /// <returns></returns>
        public static IDictionary<TKey, TValue> Sort<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            IComparer<TKey> comparer)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }
            return new SortedDictionary<TKey, TValue>(dictionary, comparer);
        }

        #endregion
    }
}
