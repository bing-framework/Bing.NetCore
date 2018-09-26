using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 设置缓存
    /// </summary>
    public interface ISetAsync
    {
        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        Task SetAsync<T>(string cacheKey, T cacheValue, TimeSpan expiry);

        /// <summary>
        /// 批量设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="values">缓存字典</param>
        /// <param name="expiry">过期时间</param>
        Task SetAllAsync<T>(IDictionary<string, T> values, TimeSpan expiry);
    }
}
