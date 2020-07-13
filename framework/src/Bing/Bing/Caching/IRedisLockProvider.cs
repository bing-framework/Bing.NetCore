using System;
using System.Threading.Tasks;

namespace Bing.Caching
{
    /// <summary>
    /// Redis 分布式锁提供程序
    /// </summary>
    public interface IRedisLockProvider
    {
        /// <summary>
        /// 锁定
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="func">函数</param>
        /// <param name="expiration">过期时间间隔</param>
        T TryLock<T>(string key, Func<T> func, TimeSpan? expiration = null);

        /// <summary>
        /// 锁定
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="func">函数</param>
        /// <param name="expiration">过期时间间隔</param>
        Task<T> TryLockAsync<T>(string key, Func<T> func, TimeSpan? expiration = null);

        /// <summary>
        /// 锁定
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="func">函数</param>
        /// <param name="expiration">过期时间间隔</param>
        Task<T> TryLockAsync<T>(string key, Func<Task<T>> func, TimeSpan? expiration = null);
    }
}
