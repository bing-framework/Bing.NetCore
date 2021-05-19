using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Helpers;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 内存缓存
    /// </summary>
    public partial class InMemoryCacheManager
    {
        /// <summary>
        /// 是否存在指定键的缓存
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            return Task.FromResult(_cache.TryGetValue(key, out var item) && item.Expired);
        }

        /// <summary>
        /// 从缓存中获取数据，如果不存在，则执行获取数据操作并添加到缓存中
        /// </summary>
        /// <typeparam name="T">缓存数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="func">获取数据操作</param>
        /// <param name="expiration">过期时间间隔</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<T> GetAsync<T>(string key, Func<Task<T>> func, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            if (_cache.TryGetValue(key, out var item) && !item.Expired)
                return (T)item.Visit();
            var value = await func.Invoke();
            await AddAsync(key, value, expiration, cancellationToken);
            return value;
        }

        /// <summary>
        /// 从缓存中获取数据
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="type">缓存数据类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            if (!_cache.TryGetValue(key, out var item) || item.Expired)
                return null;
            await Task.CompletedTask;
            return item.Visit();
        }

        /// <summary>
        /// 从缓存中获取数据
        /// </summary>
        /// <typeparam name="T">缓存数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            if (!_cache.TryGetValue(key, out var item) || item.Expired)
                return default;
            await Task.CompletedTask;
            return (T)item.Visit();
        }

        /// <summary>
        /// 当缓存数据不存在则添加，已存在不会添加，添加成功返回true
        /// </summary>
        /// <typeparam name="T">缓存数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiration">过期时间间隔</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<bool> TryAddAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            if (_cache.TryGetValue(key, out var item) && !item.Expired)
                return Task.FromResult(false);
            Add(key, value, expiration);
            return Task.FromResult(true);
        }

        /// <summary>
        /// 添加缓存。如果已存在缓存，将覆盖
        /// </summary>
        /// <typeparam name="T">缓存数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiration">过期时间间隔</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task AddAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            _cache[key] = new CacheItem(value, expiration);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 移除缓存
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Check.NotNull(key, nameof(key));
            _cache.TryRemove(key, out var _);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 通过缓存键前缀移除缓存
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            var keys = _cache.Keys.Where(x => x.StartsWith(prefix));
            foreach (var key in keys) 
                Remove(key);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            _cache.Clear();
            return Task.CompletedTask;
        }
    }
}
