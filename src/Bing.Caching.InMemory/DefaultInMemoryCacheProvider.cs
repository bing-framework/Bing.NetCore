using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Bing.Caching.CacheStats;
using Bing.Logs;
using Bing.Logs.Core;
using Bing.Utils.Helpers;
using ConcurrentCollections;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 默认内存缓存提供程序
    /// </summary>
    public class DefaultInMemoryCacheProvider:ICacheProvider
    {
        /// <summary>
        /// 内存缓存
        /// </summary>
        private readonly IMemoryCache _cache;

        /// <summary>
        /// 选项
        /// </summary>
        private readonly InMemoryOptions _options;

        /// <summary>
        /// 缓存键
        /// </summary>
        private readonly ConcurrentHashSet<string> _cacheKeys;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        private readonly CacheStatsInfo _cacheStatsInfo;

        /// <summary>
        /// 是否分布式缓存
        /// </summary>
        public bool IsDistributedCache => false;

        /// <summary>
        /// 顺序
        /// </summary>
        public int Order => _options.Order;

        /// <summary>
        /// 最大随机秒数
        /// </summary>
        public int MaxRdSecond => _options.MaxRdSecond;

        /// <summary>
        /// 缓存提供程序类型
        /// </summary>
        public CacheProviderType CacheProviderType => _options.CacheProviderType;

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        public CacheStatsInfo CacheStatsInfo => _cacheStatsInfo;

        /// <summary>
        /// 初始化一个<see cref="DefaultInMemoryCacheProvider"/>类型的实例
        /// </summary>
        /// <param name="cache">内存缓存</param>
        /// <param name="options">内存缓存选项</param>
        /// <param name="log">日志</param>
        public DefaultInMemoryCacheProvider(IMemoryCache cache, IOptionsMonitor<InMemoryOptions> options,
            ILog log = null)
        {
            this._cache = cache;
            this._options = options.CurrentValue;
            this._log = log ?? NullLog.Instance;
            this._cacheKeys = new ConcurrentHashSet<string>();
            this._cacheStatsInfo = new CacheStatsInfo();
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        public void Set<T>(string cacheKey, T cacheValue, TimeSpan expiry)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNull(cacheValue, nameof(cacheValue));
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            if (MaxRdSecond > 0)
            {
                var addSec=new Random().Next(1,MaxRdSecond);
                expiry.Add(new TimeSpan(0, 0, addSec));
            }

            _cache.Set(cacheKey, cacheValue, expiry);
            _cacheKeys.Add(cacheKey);
        }

        /// <summary>
        /// 批量设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="values">缓存字典</param>
        /// <param name="expiry">过期时间</param>
        public void SetAll<T>(IDictionary<string, T> values, TimeSpan expiry)
        {
            Check.NotNegativeOrZero(expiry,nameof(expiry));
            Check.NotNullOrEmpty(values, nameof(values));
            foreach (var value in values)
            {
                this.Set(value.Key,value.Value,expiry);
            }
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        public async Task SetAsync<T>(string cacheKey, T cacheValue, TimeSpan expiry)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNull(cacheValue, nameof(cacheValue));
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            if (MaxRdSecond > 0)
            {
                var addSec = new Random().Next(1, MaxRdSecond);
                expiry.Add(new TimeSpan(0, 0, addSec));
            }

            await Task.Run(() =>
            {
                _cache.Set(cacheKey, cacheValue, expiry);
                _cacheKeys.Add(cacheKey);
            });

        }

        /// <summary>
        /// 批量设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="values">缓存字典</param>
        /// <param name="expiry">过期时间</param>
        public async Task SetAllAsync<T>(IDictionary<string, T> values, TimeSpan expiry)
        {
            Check.NotNegativeOrZero(expiry, nameof(expiry));
            Check.NotNullOrEmpty(values, nameof(values));

            var tasks=new List<Task>();
            foreach (var value in values)
            {
                tasks.Add(SetAsync(value.Key, value.Value, expiry));
            }
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存类型</param>
        /// <returns></returns>
        public CacheValue<T> Get<T>(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            if (_cache.Get(cacheKey) is T result)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            return CacheValue<T>.NoValue;
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="dataRetriever">数据检索器</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        public CacheValue<T> Get<T>(string cacheKey, Func<T> dataRetriever, TimeSpan expiry) where T : class
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNegativeOrZero(expiry,nameof(expiry));

            if (_cache.Get(cacheKey) is T result)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            result = dataRetriever?.Invoke();
            if (result != null)
            {
                Set(cacheKey, result, expiry);
                return new CacheValue<T>(result,true);
            }
            return CacheValue<T>.NoValue;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKeys">缓存键集合</param>
        /// <returns></returns>
        public IDictionary<string, CacheValue<T>> GetAll<T>(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"GetAll : cacheKeys = {string.Join(",", cacheKeys)}");

            var map = new Dictionary<string, CacheValue<T>>();
            foreach (var cacheKey in cacheKeys)
            {
                map[cacheKey] = Get<T>(cacheKey);
            }
            return map;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public IDictionary<string, CacheValue<T>> GetByPrefix<T>(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));
            WriteLog($"GetByPrefix : prefix = {prefix}");

            var map = new Dictionary<string, CacheValue<T>>();
            var keys = _cacheKeys.Where(x => x.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));

            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    map[key] = Get<T>(key);
                }
            }

            return map;
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存类型</param>
        /// <returns></returns>
        public async Task<CacheValue<T>> GetAsync<T>(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            var result = await Task.FromResult((T) _cache.Get(cacheKey));

            if (result != null)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            return CacheValue<T>.NoValue;
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="dataRetriever">数据检索器</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        public async Task<CacheValue<T>> GetAsync<T>(string cacheKey, Func<Task<T>> dataRetriever, TimeSpan expiry) where T : class
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            if (_cache.Get(cacheKey) is T result)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            result =await dataRetriever?.Invoke();
            if (result != null)
            {
                Set(cacheKey, result, expiry);
                return new CacheValue<T>(result, true);
            }
            return CacheValue<T>.NoValue;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKeys">缓存键集合</param>
        /// <returns></returns>
        public Task<IDictionary<string, CacheValue<T>>> GetAllAsync<T>(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"GetAllAsync : cacheKeys = {string.Join(",", cacheKeys)}");

            var map = new Dictionary<string, Task<CacheValue<T>>>();
            foreach (var cacheKey in cacheKeys)
            {
                map[cacheKey] = GetAsync<T>(cacheKey);
            }

            return Task.WhenAll(map.Values).ContinueWith<IDictionary<string, CacheValue<T>>>(
                t => map.ToDictionary(k => k.Key, v => v.Value.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public Task<IDictionary<string, CacheValue<T>>> GetByPrefixAsync<T>(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));
            WriteLog($"GetByPrefixAsync : prefix = {prefix}");

            var map = new Dictionary<string, Task<CacheValue<T>>>();
            var keys = _cacheKeys.Where(x => x.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));

            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    map[key] = GetAsync<T>(key);
                }
            }

            return Task.WhenAll(map.Values).ContinueWith<IDictionary<string, CacheValue<T>>>(
                t => map.ToDictionary(k => k.Key, v => v.Value.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public void Remove(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            WriteLog($"Remove : cacheKey = {cacheKey}");

            _cache.Remove(cacheKey);
            _cacheKeys.TryRemove(cacheKey);
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public void RemoveByPrefix(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));
            WriteLog($"RemoveByPrefix : prefix = {prefix}");

            var keys = _cacheKeys.Where(x => x.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));
            if (keys.Any())
            {
                foreach (var key in keys)
                {
                    this.Remove(key);
                }
            }
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public void RemoveAll(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));

            WriteLog($"RemoveAll : cacheKeys = {string.Join(",", cacheKeys)}");

            foreach (var key in cacheKeys.Distinct())
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public async Task RemoveAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            WriteLog($"RemoveAsync : cacheKey = {cacheKey}");

            await Task.Run(() =>
            {
                _cache.Remove(cacheKey);
                _cacheKeys.TryRemove(cacheKey);
            });
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));
            WriteLog($"RemoveByPrefixAsync : prefix = {prefix}");

            var keys = _cacheKeys.Where(x => x.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));
            if (keys.Any())
            {
                var tasks = new List<Task>();
                foreach (var key in keys)
                {
                    tasks.Add(this.RemoveAsync(key));
                }

                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public async Task RemoveAllAsync(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));

            WriteLog($"RemoveAllAsync : cacheKeys = {string.Join(",", cacheKeys)}");

            var tasks=new List<Task>();
            foreach (var key in cacheKeys.Distinct())
            {
                tasks.Add(this.RemoveAsync(key));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public bool Exists(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey,nameof(cacheKey));

            return _cache.TryGetValue(cacheKey, out var value);
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            return await Task.FromResult(_cache.TryGetValue(cacheKey, out var value));
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        public void Refresh<T>(string cacheKey, T cacheValue, TimeSpan expiry)
        {
            Check.NotNullOrEmpty(cacheKey,nameof(cacheKey));
            Check.NotNull(cacheValue,nameof(cacheValue));
            Check.NotNegativeOrZero(expiry,nameof(expiry));

            this.Remove(cacheKey);
            this.Set(cacheKey,cacheValue,expiry);
        }

        /// <summary>
        /// 刷新缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKey">缓存键</param>
        /// <param name="cacheValue">缓存值</param>
        /// <param name="expiry">过期时间</param>
        public async Task RefreshAsync<T>(string cacheKey, T cacheValue, TimeSpan expiry)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNull(cacheValue, nameof(cacheValue));
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            await this.RemoveAsync(cacheKey);
            await this.SetAsync(cacheKey, cacheValue, expiry);
        }

        /// <summary>
        /// 获取缓存数量
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public int GetCount(string prefix = "")
        {
            return string.IsNullOrWhiteSpace(prefix)
                ? _cacheKeys.Count
                : _cacheKeys.Count(x => x.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Flush()
        {
            WriteLog($"Flush");

            foreach (var key in _cacheKeys)
            {
                _cache.Remove(key);
            }

            _cacheKeys.Clear();
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task FlushAsync()
        {
            WriteLog($"FlushAsync");

            Flush();

            await Task.CompletedTask;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="message">消息</param>
        private void WriteLog(string message)
        {
            if (_options.EnableLogging)
            {
                _log.Info(message);
            }
        }
    }
}
