using System.Collections.Generic;
using Bing.Core.Data;
using Bing.Release;

namespace Bing.Caching
{
    /// <summary>
    /// 单类型缓存
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public interface ISingleTypeCache<TKey, TValue> : IGetable<TKey, TValue>, IClearable, IReader<IDictionary<TKey, TValue>>
    {
        /// <summary>
        /// 缓存键数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 添加。如果存在则不添加，返回false
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        bool Add(TKey key, TValue value);

        /// <summary>
        /// 更新。如果不存在则不更新，返回false
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        bool Update(TKey key, TValue value);

        /// <summary>
        /// 设置。如果村则则更新，不存在则添加
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        bool Set(TKey key, TValue value);

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="key">键</param>
        bool Remove(TKey key);

        /// <summary>
        /// 移除集合
        /// </summary>
        /// <param name="keys">键数组</param>
        bool Remove(TKey[] keys);

        /// <summary>
        /// 判断是否存在
        /// </summary>
        /// <param name="key">键</param>
        bool Exists(TKey key);
    }
}
