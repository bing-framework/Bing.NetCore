using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Bing.Helpers;

namespace Bing.Locks
{
    /// <summary>
    /// 本地锁
    /// </summary>
    public class LocalLock : ILock
    {
        /// <summary>
        /// 锁缓存
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly ConcurrentDictionary<string, object> _lockCache = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 用户锁缓存
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly ConcurrentDictionary<string, string> _lockUserCache = new ConcurrentDictionary<string, string>();

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

            var obj = _lockCache.GetOrAdd(key, k => new object());
            if (Monitor.TryEnter(obj, expiration))
            {
                _lockUserCache[key] = value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取一个锁（需要自己释放）
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <param name="expiration">锁定时间间隔</param>
        /// <returns>true:成功锁定; false:之前已被锁定</returns>
        public Task<bool> LockTakeAsync(string key, string value, TimeSpan expiration) => Task.FromResult(LockTake(key, value, expiration));

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

            _lockCache.TryGetValue(key, out var obj);
            if (obj != null)
            {
                if (_lockUserCache[key] == value)
                {
                    Monitor.Exit(obj);
                    return true;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// 释放一个锁
        /// </summary>
        /// <param name="key">锁定标识</param>
        /// <param name="value">当前占用值</param>
        /// <returns>true:释放成功; false:释放失败</returns>
        public Task<bool> LockReleaseAsync(string key, string value) => Task.FromResult(LockRelease(key, value));

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
            if (LockTake(key, value, expiration))
            {
                try
                {
                    executeAction();
                }
                finally
                {
                    LockRelease(key, value);
                }
            }
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
            if (await LockTakeAsync(key, value, expiration))
            {
                try
                {
                    await executeAction();
                }
                finally
                {
                    await LockReleaseAsync(key, value);
                }
            }
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
            if (LockTake(key, value, expiration))
            {
                try
                {
                    return executeAction();
                }
                finally
                {
                    LockRelease(key, value);
                }
            }
            return defaultValue;
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
        public async Task<T> ExecuteWithLockAsync<T>(string key, string value, TimeSpan expiration, Func<Task<T>> executeAction, T defaultValue = default)
        {
            if (executeAction == null)
                return defaultValue;
            if (await LockTakeAsync(key, value, expiration))
            {
                try
                {
                    return await executeAction();
                }
                finally
                {
                    await LockReleaseAsync(key, value);
                }
            }
            return defaultValue;
        }
    }
}
