using System;
using System.Threading.Tasks;
using Bing.Helpers;

namespace Bing.Locks
{
    /// <summary>
    /// 基于
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class CSRedisDistributedLock : IDistributedLock
    {
        /// <summary>
        /// 获取一个锁（需要自己释放）
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <returns>true:成功锁定; false:之前已被锁定</returns>
        public bool LockTake(string key, string value, TimeSpan expiration)
        {
            Check.NotNull(key, nameof(key));
            Check.NotNull(value, nameof(value));
            Check.NotNull(expiration, nameof(expiration));
            return RedisHelper.Set(key, value, expiration);
        }

        /// <summary>
        /// 获取一个锁（需要自己释放）
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <returns>true:成功锁定; false:之前已被锁定</returns>
        public async Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration)
        {
            Check.NotNull(key, nameof(key));
            Check.NotNull(value, nameof(value));
            Check.NotNull(expiration, nameof(expiration));
            return await RedisHelper.SetAsync(key, value, expiration);
        }

        /// <summary>
        /// 释放一个锁
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <returns>true:释放成功; false:释放失败</returns>
        public bool LockRelease(string key, string value)
        {
            Check.NotNull(key, nameof(key));
            Check.NotNull(value, nameof(value));
            RedisHelper.Del(key);
            return true;
        }

        /// <summary>
        /// 释放一个锁
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <returns>true:释放成功; false:释放失败</returns>
        public async Task<bool> LockReleaseAsync(string key, string value)
        {
            Check.NotNull(key, nameof(key));
            Check.NotNull(value, nameof(value));
            await RedisHelper.DelAsync(key);
            return true;
        }

        /// <summary>
        /// 使用锁执行一个方法
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <param name="executeAction">执行的方法</param>
        public void ExecuteWithLock(string key, string value, TimeSpan expiration, Action executeAction)
        {
            if (executeAction == null)
                return;
            using (RedisHelper.Lock(key, expiration.Seconds))
                executeAction();
        }

        /// <summary>
        /// 使用锁执行一个方法
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <param name="executeAction">执行的方法</param>
        public async Task ExecuteWithLockAsync(string key, string value, TimeSpan expiration, Func<Task> executeAction)
        {
            if (executeAction == null)
                return;
            using (RedisHelper.Lock(key, expiration.Seconds))
                await executeAction();
        }

        /// <summary>
        /// 使用锁执行一个方法
        /// </summary>
        /// <typeparam name="T">返回对象类型</typeparam>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <param name="executeAction">执行的方法</param>
        /// <param name="defaultValue">默认值</param>
        public T ExecuteWithLock<T>(string key, string value, TimeSpan expiration, Func<T> executeAction, T defaultValue = default)
        {
            if (executeAction == null)
                return defaultValue;
            using (RedisHelper.Lock(key, expiration.Seconds))
                return executeAction();
        }

        /// <summary>
        /// 使用锁执行一个方法
        /// </summary>
        /// <typeparam name="T">返回对象类型</typeparam>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <param name="executeAction">执行的方法</param>
        /// <param name="defaultValue">默认值</param>
        public async Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction,
            T defaultValue = default)
        {
            if (executeAction == null)
                return defaultValue;
            using (RedisHelper.Lock(key, expiration.Seconds))
                return await executeAction();
        }
    }
}
