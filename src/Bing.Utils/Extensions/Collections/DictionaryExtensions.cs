using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字典(<see cref="IDictionary{TKey,TValue}"/>) 扩展
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

        /// <summary>
        /// 对指定的字典进行排序，根据值元素进行排序
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static IDictionary<TKey, TValue> SortByValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return new SortedDictionary<TKey, TValue>(dictionary).OrderBy(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        #endregion

        #region ToQueryString(将字典转换成查询字符串)

        /// <summary>
        /// 将字典转换成查询字符串
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static string ToQueryString<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null || !dictionary.Any())
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in dictionary)
            {
                sb.Append($"{item.Key.ToString()}={item.Value.ToString()}&");
            }

            sb.TrimEnd("&");
            return sb.ToString();
        }

        #endregion

        #region GetKey(根据Value反向查找Key)

        /// <summary>
        /// 根据Value反向查找Key
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static TKey GetKey<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TValue value)
        {
            foreach (var item in dictionary.Where(x=>x.Value.Equals(value)))
            {
                return item.Key;
            }

            return default(TKey);
        }

        #endregion

        #region TryAdd(尝试添加键值对到字典)

        /// <summary>
        /// 尝试将键值对添加到字典中。如果不存在，则添加；存在，不添加也不抛异常
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static IDictionary<TKey, TValue> TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            TKey key, TValue value)
        {
            if (!dictionary.ContainsKey(key))
            {
                dictionary.Add(key,value);
            }

            return dictionary;
        }

        #endregion

        #region ToHashTable(将字典转换成哈希表)

        /// <summary>
        /// 将字典转换成哈希表
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static Hashtable ToHashTable<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            var table=new Hashtable();
            foreach (var item in dictionary)
            {
                table.Add(item.Key,item.Value);
            }

            return table;
        }

        #endregion

        #region Invert(字典颠倒)

        /// <summary>
        /// 对指定字典进行颠倒键值对，创建新字典（值为键，键为值）
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static IDictionary<TValue, TKey> Reverse<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            return dictionary.ToDictionary(x => x.Value, x => x.Key);
        }

        #endregion

        #region AsReadOnly(转换成只读字典)

        /// <summary>
        /// 转换成只读字典
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="dictionary">字典</param>
        /// <returns></returns>
        public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }

        #endregion

        #region EqualsTo(判断两个字典中的元素是否相等)

        /// <summary>
        /// 判断两个字典中的元素是否相等
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="sourceDict">源字典</param>
        /// <param name="targetDict">目标字典</param>
        /// <exception cref="ArgumentNullException">源字典对象为空、目标字典对象为空</exception>
        public static bool EqualsTo<TKey, TValue>(this IDictionary<TKey, TValue> sourceDict,
            IDictionary<TKey, TValue> targetDict)
        {
            if (sourceDict == null)
                throw new ArgumentNullException(nameof(sourceDict), $@"源字典对象不可为空！");
            if (targetDict == null)
                throw new ArgumentNullException(nameof(sourceDict), $@"目标字典对象不可为空！");
            // 长度对比
            if (sourceDict.Count != targetDict.Count)
                return false;
            if (!sourceDict.Any() && !targetDict.Any())
                return true;
            // 深度对比
            var sourceKeyValues = sourceDict.OrderBy(x => x.Key).ToArray();
            var targetKeyValues = targetDict.OrderBy(x => x.Key).ToArray();
            var sourceKeys = sourceKeyValues.Select(x => x.Key);
            var targetKeys = targetKeyValues.Select(x => x.Key);
            var sourceValues = sourceKeyValues.Select(x => x.Value);
            var targetValues = targetKeyValues.Select(x => x.Value);
            if (sourceKeys.EqualsTo(targetKeys) && sourceValues.EqualsTo(targetValues))
                return true;
            return false;
        }

        #endregion
    }
}
