using System;
using System.Collections.Generic;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 获取缓存
    /// </summary>
    public interface IGet
    {
        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存类型</param>
        /// <returns></returns>
        CacheValue<T> Get<T>(string cacheKey);

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="dataRetriever">数据检索器</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        CacheValue<T> Get<T>(string cacheKey, Func<T> dataRetriever, TimeSpan expiry) where T : class;

        /// <summary>
        /// 批量获取缓存，根据缓存键
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKeys">缓存键集合</param>
        /// <returns></returns>
        IDictionary<string, CacheValue<T>> GetAll<T>(IEnumerable<string> cacheKeys);

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        IDictionary<string, CacheValue<T>> GetByPrefix<T>(string prefix);
    }
}
