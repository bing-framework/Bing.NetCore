using System;

namespace Bing.Caching
{
    /// <summary>
    /// 缓存管理
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// 获取key的数据，不存在则读取数据做一次缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="getData">获取数据的方法</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        T GetOrAdd<T>(string key, Func<T> getData, TimeSpan? expiry = null);

        /// <summary>
        /// 获取key的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        T Get<T>(string key);

        /// <summary>
        /// 设置数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        bool Set<T>(string key, T value, TimeSpan? expiry = null);

        /// <summary>
        /// 删除指定key
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        bool Delete(string key);
    }
}
