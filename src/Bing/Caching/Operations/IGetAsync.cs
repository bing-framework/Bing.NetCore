using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 获取缓存
    /// </summary>
    public interface IGetAsync
    {
        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存类型</param>
        /// <returns></returns>
        Task<CacheValue<T>> GetAsync<T>(string cacheKey);

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="dataRetriever">数据检索器</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        Task<CacheValue<T>> GetAsync<T>(string cacheKey, Func<Task<T>> dataRetriever, TimeSpan expiry) where T : class;

        /// <summary>
        /// 批量获取缓存，根据缓存键
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKeys">缓存键集合</param>
        /// <returns></returns>
        Task<IDictionary<string, CacheValue<T>>> GetAllAsync<T>(IEnumerable<string> cacheKeys);

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        Task<IDictionary<string, CacheValue<T>>> GetByPrefixAsync<T>(string prefix);
    }
}
