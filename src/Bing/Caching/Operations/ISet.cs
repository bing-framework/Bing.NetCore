using System;
using System.Collections.Generic;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 设置缓存
    /// </summary>
    public interface ISet
    {
        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        void Set<T>(string cacheKey, T cacheValue, TimeSpan expiry);

        /// <summary>
        /// 批量设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="values">缓存字典</param>
        /// <param name="expiry">过期时间</param>
        void SetAll<T>(IDictionary<string, T> values, TimeSpan expiry);
    }
}
