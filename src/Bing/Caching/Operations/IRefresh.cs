using System;

namespace Bing.Caching.Operations
{
    /// <summary>
    /// 刷新缓存
    /// </summary>
    public interface IRefresh
    {
        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        void Refresh<T>(string cacheKey, T cacheValue, TimeSpan expiry);

    }
}
