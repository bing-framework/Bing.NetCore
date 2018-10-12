using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bing.Caching.CacheStats;
using Bing.Logs.Core;
using Bing.Utils.Helpers;
using Enyim.Caching;
using Enyim.Caching.Memcached;
using Microsoft.Extensions.Options;

namespace Bing.Caching.Memcached
{
    /// <summary>
    /// 默认 Memcached 缓存提供程序
    /// </summary>
    public class DefaultMemcachedCacheProvider:ICacheProvider
    {
        /// <summary>
        /// Memcached客户端
        /// </summary>
        private readonly IMemcachedClient _memcachedClient;

        /// <summary>
        /// Memcached选项
        /// </summary>
        private readonly MemcachedOptions _options;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly Bing.Logs.ILog _log;

        public string Name { get; }

        /// <summary>
        /// 是否分布式缓存
        /// </summary>
        public bool IsDistributedCache => true;

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
        public CacheStatsInfo CacheStatsInfo { get; }

        /// <summary>
        /// 初始化一个<see cref="DefaultMemcachedCacheProvider"/>类型的实例
        /// </summary>
        /// <param name="memcachedClient">Memcached客户端</param>
        /// <param name="options">Memcached选项</param>
        /// <param name="log">日志</param>
        public DefaultMemcachedCacheProvider(IMemcachedClient memcachedClient,
            IOptionsMonitor<MemcachedOptions> options, Bing.Logs.ILog log = null)
        {
            this._memcachedClient = memcachedClient;
            this._options = options.CurrentValue;
            this._log = log ?? NullLog.Instance;
            this.CacheStatsInfo = new CacheStatsInfo();
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
                var addSec = new Random().Next(1, MaxRdSecond);
                expiry.Add(new TimeSpan(0, 0, addSec));
            }

            _memcachedClient.Store(StoreMode.Set, this.HandleCacheKey(cacheKey), cacheValue, expiry);
        }

        /// <summary>
        /// 处理缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        private string HandleCacheKey(string cacheKey)
        {
            // Memcached 有250个字符的长度限制
            // 参考 memcached.h in https://github.com/memcached/memcached/
            if (cacheKey.Length >= 250)
            {
                using (SHA1 sha1=SHA1.Create())
                {
                    byte[] data = sha1.ComputeHash(Encoding.UTF8.GetBytes(cacheKey));
                    return Convert.ToBase64String(data, Base64FormattingOptions.None);
                }
            }

            return cacheKey;
        }

        /// <summary>
        /// 批量设置缓存
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="values">缓存字典</param>
        /// <param name="expiry">过期时间</param>
        public void SetAll<T>(IDictionary<string, T> values, TimeSpan expiry)
        {
            Check.NotNegativeOrZero(expiry, nameof(expiry));
            Check.NotNullOrEmpty(values, nameof(values));

            foreach (var value in values)
            {
                Set(value.Key, value.Value, expiry);
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

            await _memcachedClient.StoreAsync(StoreMode.Set, this.HandleCacheKey(cacheKey), cacheValue, expiry);
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

            var tasks = new List<Task>();

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

            if (_memcachedClient.Get(this.HandleCacheKey(cacheKey)) is T result)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result,true);
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
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            if (_memcachedClient.Get(this.HandleCacheKey(cacheKey)) is T result)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            var item = dataRetriever?.Invoke();
            if (item != null)
            {
                this.Set(cacheKey,item,expiry);
                return new CacheValue<T>(item, true);
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

            var values = _memcachedClient.Get<T>(cacheKeys);
            var result = new Dictionary<string, CacheValue<T>>();

            foreach (var value in values)
            {
                if (value.Value != null)
                {
                    result.Add(value.Key, new CacheValue<T>(value.Value, true));
                }
                else
                {
                    result.Add(value.Key,CacheValue<T>.NoValue);
                }
            }

            return result;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public IDictionary<string, CacheValue<T>> GetByPrefix<T>(string prefix)
        {
            // TODO:暂未实现
            throw new NotImplementedException();
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

            var result = await _memcachedClient.GetValueAsync<T>(this.HandleCacheKey(cacheKey));

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

            var result = await _memcachedClient.GetValueAsync<T>(this.HandleCacheKey(cacheKey));

            if (result!=null)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                return new CacheValue<T>(result, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            var item = await dataRetriever?.Invoke();
            if (item != null)
            {
                await this.SetAsync(cacheKey, item, expiry);
                return new CacheValue<T>(item, true);
            }

            return CacheValue<T>.NoValue;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="cacheKeys">缓存键集合</param>
        /// <returns></returns>
        public async Task<IDictionary<string, CacheValue<T>>> GetAllAsync<T>(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"GetAllAsync : cacheKeys = {string.Join(",", cacheKeys)}");

            var values = await _memcachedClient.GetAsync<T>(cacheKeys);
            var result = new Dictionary<string, CacheValue<T>>();

            foreach (var value in values)
            {
                if (value.Value != null)
                {
                    result.Add(value.Key, new CacheValue<T>(value.Value, true));
                }
                else
                {
                    result.Add(value.Key, CacheValue<T>.NoValue);
                }
            }

            return result;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public Task<IDictionary<string, CacheValue<T>>> GetByPrefixAsync<T>(string prefix)
        {
            // TODO:暂未实现
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public void Remove(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            WriteLog($"Remove : cacheKey = {cacheKey}");

            _memcachedClient.Remove(this.HandleCacheKey(cacheKey));
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <remarks>
        /// Before using the method , you should follow this link 
        /// https://github.com/memcached/memcached/wiki/ProgrammingTricks#namespacing
        /// and confirm that you use the namespacing when you set and get the cache.
        /// </remarks>
        /// <param name="prefix">缓存键前缀</param>
        public void RemoveByPrefix(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            var oldPrefixKey = _memcachedClient.Get(prefix)?.ToString();

            var newValue = DateTime.UtcNow.Ticks.ToString();
            WriteLog($"RemoveByPrefix : prefix = {prefix}");

            if (oldPrefixKey.Equals(newValue))
            {
                newValue = string.Concat(newValue, new Random().Next(9).ToString());
            }

            _memcachedClient.Store(StoreMode.Set, this.HandleCacheKey(prefix), newValue, new TimeSpan(0, 0, 0));
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
                Remove(key);
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

            await _memcachedClient.RemoveAsync(this.HandleCacheKey(cacheKey));
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            var oldPrefixKey = _memcachedClient.Get(prefix)?.ToString();

            var newValue = DateTime.UtcNow.Ticks.ToString();
            WriteLog($"RemoveByPrefixAsync : prefix = {prefix}");

            if (oldPrefixKey.Equals(newValue))
            {
                newValue = string.Concat(newValue, new Random().Next(9).ToString());
            }

            await _memcachedClient.StoreAsync(StoreMode.Set, this.HandleCacheKey(prefix), newValue, new TimeSpan(0, 0, 0));
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public async Task RemoveAllAsync(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"RemoveAllAsync : cacheKeys = {string.Join(",", cacheKeys)}");

            var tasks = new List<Task>();
            foreach (var key in cacheKeys.Distinct())
            {
                tasks.Add(RemoveAsync(key));
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
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            return _memcachedClient.TryGet(this.HandleCacheKey(cacheKey), out var obj);
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            return await Task.FromResult(_memcachedClient.TryGet(this.HandleCacheKey(cacheKey), out var obj));
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
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            Check.NotNull(cacheValue, nameof(cacheValue));
            Check.NotNegativeOrZero(expiry, nameof(expiry));

            this.Remove(cacheKey);
            this.Set(cacheKey, cacheValue, expiry);
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
            if (string.IsNullOrWhiteSpace(prefix))
            {
                // 不准确，有时Memecached只会使缓存过期但不会立即释放或刷新内存
                return int.Parse(_memcachedClient.Stats().GetRaw("curr_items").FirstOrDefault().Value);
            }

            return 0;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Flush()
        {
            WriteLog($"Flush");
            // 不立即刷新内存，只会导致所有项目过期
            _memcachedClient.FlushAll();
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task FlushAsync()
        {
            WriteLog($"FlushAsync");

            await _memcachedClient.FlushAllAsync();
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
