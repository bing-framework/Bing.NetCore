using System;
using System.Collections.Concurrent;
using Bing.Logs;
using Microsoft.Extensions.Caching.Memory;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 默认内存缓存管理器
    /// </summary>
    public class DefaultInMemoryCacheManager:IInMemoryCacheManager
    {
        /// <summary>
        /// 缓存同步管道
        /// </summary>
        private const string CacheSyncChannel = "cache.sync";

        /// <summary>
        /// 缓存级别，本地内存缓存
        /// </summary>
        public CacheLevel CacheLevel => CacheLevel.Local;

        /// <summary>
        /// 内存缓存
        /// </summary>
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// 缓存键无效期字典
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> _keyNullExpiry;

        /// <summary>
        /// 初始化一个<see cref="DefaultInMemoryCacheManager"/>类型的实例
        /// </summary>
        /// <param name="memoryCache">内存缓存</param>
        /// <param name="log">日志</param>
        public DefaultInMemoryCacheManager(IMemoryCache memoryCache, ILog log)
        {
            _memoryCache = memoryCache;
            _log = log;
            _keyNullExpiry = new ConcurrentDictionary<string, DateTime>();
        }

        /// <summary>
        /// 获取key的数据，不存在则读取数据做一次缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="getData">获取数据的方法</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        public T GetOrAdd<T>(string key, Func<T> getData, TimeSpan? expiry = null)
        {
            var nullKey = $"{key}:NullData";

            // 防止缓存雪崩
            if (_keyNullExpiry.TryGetValue(nullKey, out var expiryTime) && expiryTime >= DateTime.Now)
            {
                return default(T);
            }

            var value = Get<T>(key);
            if (value == null || value.Equals(default(T)))
            {
                value = getData();

                // 防止缓存雪崩
                if (value == null)
                {
                    var now = DateTime.Now.AddSeconds(5);
                    if (!_keyNullExpiry.TryAdd(nullKey, now))
                    {
                        _keyNullExpiry[nullKey] = now;
                    }
                }
                else
                {
                    Set(key, value, expiry);
                }
            }

            return value;
        }

        /// <summary>
        /// 获取key的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            return _memoryCache.Get<T>(key);
        }

        /// <summary>
        /// 设置数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan? expiry = null)
        {
            return SetAndPublish(key, value, expiry);
        }

        /// <summary>
        /// 设置单个数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        private bool SetAndPublish<T>(string key,T value,TimeSpan? expiry=null)
        {
            if (expiry.HasValue)
            {
                _memoryCache.Set(key, value, expiry.Value);
            }
            else
            {
                _memoryCache.Set(key, value);
            }

            return true;
        }

        /// <summary>
        /// 删除指定key
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        public bool Delete(string key)
        {
            Remove(key);
            return true;
        }

        /// <summary>
        /// 删除单个数据
        /// </summary>
        /// <param name="key">缓存键</param>
        private void Remove(string key)
        {
            _memoryCache.Remove(key);
        }
    }
}
