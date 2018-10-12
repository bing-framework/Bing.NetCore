using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Caching.CacheStats;
using Bing.Utils.Helpers;

namespace Bing.Caching.Hybrid
{
    /// <summary>
    /// 默认混合缓存提供程序
    /// </summary>
    public class DefaultHybridCacheProvider:IHybridCacheProvider
    {
        /// <summary>
        /// 缓存提供程序列表
        /// </summary>
        private readonly IEnumerable<ICacheProvider> _providers;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name => throw new NotImplementedException();

        /// <summary>
        /// 是否分布式缓存
        /// </summary>
        public bool IsDistributedCache => throw new NotImplementedException();

        /// <summary>
        /// 顺序
        /// </summary>
        public int Order => throw new NotImplementedException();

        /// <summary>
        /// 最大随机秒数
        /// </summary>
        public int MaxRdSecond => throw new NotImplementedException();

        /// <summary>
        /// 缓存提供程序类型
        /// </summary>
        public CacheProviderType CacheProviderType => throw new NotImplementedException();

        /// <summary>
        /// 缓存统计信息
        /// </summary>
        public CacheStatsInfo CacheStatsInfo => throw new NotImplementedException();

        /// <summary>
        /// 初始化一个<see cref="DefaultHybridCacheProvider"/>类型的实例
        /// </summary>
        /// <param name="providers">缓存提供程序列表</param>
        public DefaultHybridCacheProvider(IEnumerable<ICacheProvider> providers)
        {
            if (providers == null || !providers.Any())
            {
                throw new ArgumentNullException(nameof(providers));
            }

            // 混合缓存，2-3级足够
            if (providers.Count() > 3)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            this._providers = providers.OrderBy(x => x.Order);

            // TODO:本地缓存应该订阅远程缓存
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

            foreach (var provider in _providers)
            {
                provider.Set(cacheKey,cacheValue,expiry);
            }
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

            foreach (var provider in _providers)
            {
                provider.SetAll(values,expiry);
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

            var tasks=new List<Task>();

            foreach (var provider in _providers)
            {
                tasks.Add(provider.SetAsync(cacheKey, cacheValue, expiry));
            }

            await Task.WhenAll(tasks);
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

            foreach (var provider in _providers)
            {
                tasks.Add(provider.SetAllAsync(values, expiry));
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

            CacheValue<T> cachedValue = null;
            foreach (var provider in _providers)
            {
                cachedValue = provider.Get<T>(cacheKey);
                if (cachedValue.HasValue)
                {
                    break;
                }
            }

            if (!cachedValue.HasValue)
            {
                return CacheValue<T>.NoValue;
            }

            return cachedValue;
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

            CacheValue<T> cachedValue = null;
            foreach (var provider in _providers)
            {
                cachedValue = provider.Get(cacheKey, dataRetriever, expiry);
                if (cachedValue.HasValue)
                {
                    break;
                }
            }

            if (!cachedValue.HasValue)
            {
                var retriever = dataRetriever?.Invoke();
                if (retriever != null)
                {
                    Set(cacheKey,retriever,expiry);
                    return new CacheValue<T>(retriever,true);
                }
                return CacheValue<T>.NoValue;
            }

            return cachedValue;
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

            var local = _providers.FirstOrDefault();
            var localDict = local.GetAll<T>(cacheKeys);

            // 找不到本地缓存的缓存键
            var localNotFindKeys = localDict.Where(x => !x.Value.HasValue).Select(x => x.Key);

            if (!localNotFindKeys.Any())
            {
                return localDict;
            }

            foreach (var item in localNotFindKeys)
            {
                localDict.Remove(item);
            }

            // 远程缓存
            foreach (var provider in _providers.Skip(1))
            {
                var disDict = provider.GetAll<T>(localNotFindKeys);
                localDict.Concat(disDict).ToDictionary(k => k.Key, v => v.Value);
            }

            return localDict;
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

            var local = _providers.FirstOrDefault();
            var localDict = local.GetByPrefix<T>(prefix);

            // 找不到本地缓存的缓存键
            var localNotFindKeys = localDict.Where(x => !x.Value.HasValue).Select(x => x.Key);

            if (!localNotFindKeys.Any())
            {
                return localDict;
            }

            foreach (var item in localNotFindKeys)
            {
                localDict.Remove(item);
            }

            // 远程缓存
            foreach (var provider in _providers.Skip(1))
            {
                var disDict = provider.GetAll<T>(localNotFindKeys);
                localDict.Concat(disDict).ToDictionary(k => k.Key, v => v.Value);
            }

            return localDict;
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

            CacheValue<T> cachedValue = null;
            foreach (var provider in _providers)
            {
                cachedValue = await provider.GetAsync<T>(cacheKey);
                if (cachedValue.HasValue)
                {
                    break;
                }
            }

            if (!cachedValue.HasValue)
            {
                return CacheValue<T>.NoValue;
            }

            return await Task.FromResult(cachedValue);
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

            CacheValue<T> cachedValue = null;
            foreach (var provider in _providers)
            {
                cachedValue = await provider.GetAsync(cacheKey, dataRetriever, expiry);
                if (cachedValue.HasValue)
                {
                    break;
                }
            }

            if (!cachedValue.HasValue)
            {
                var retriever = await dataRetriever?.Invoke();
                if (retriever != null)
                {
                    await SetAsync(cacheKey, retriever, expiry);
                    return new CacheValue<T>(retriever, true);
                }
                return CacheValue<T>.NoValue;
            }

            return cachedValue;
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

            var local = _providers.FirstOrDefault();
            var localDict = await local.GetAllAsync<T>(cacheKeys);

            // 找不到本地缓存的缓存键
            var localNotFindKeys = localDict.Where(x => !x.Value.HasValue).Select(x => x.Key);

            if (!localNotFindKeys.Any())
            {
                return localDict;
            }

            foreach (var item in localNotFindKeys)
            {
                localDict.Remove(item);
            }

            // 远程缓存
            foreach (var provider in _providers.Skip(1))
            {
                var disDict = await provider.GetAllAsync<T>(localNotFindKeys);
                localDict.Concat(disDict).ToDictionary(k => k.Key, v => v.Value);
            }

            return localDict;
        }

        /// <summary>
        /// 批量获取缓存，根据缓存键前缀
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="prefix">缓存键前缀</param>
        /// <returns></returns>
        public async Task<IDictionary<string, CacheValue<T>>> GetByPrefixAsync<T>(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            var local = _providers.FirstOrDefault();
            var localDict = await local.GetByPrefixAsync<T>(prefix);

            // 找不到本地缓存的缓存键
            var localNotFindKeys = localDict.Where(x => !x.Value.HasValue).Select(x => x.Key);

            if (!localNotFindKeys.Any())
            {
                return localDict;
            }

            foreach (var item in localNotFindKeys)
            {
                localDict.Remove(item);
            }

            // 远程缓存
            foreach (var provider in _providers.Skip(1))
            {
                var disDict = await provider.GetAllAsync<T>(localNotFindKeys);
                localDict.Concat(disDict).ToDictionary(k => k.Key, v => v.Value);
            }

            return localDict;
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public void Remove(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            foreach (var provider in _providers)
            {
                provider.Remove(cacheKey);
            }
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public void RemoveByPrefix(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            foreach (var provider in _providers)
            {
                provider.RemoveByPrefix(prefix);
            }
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public void RemoveAll(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));

            foreach (var provider in _providers)
            {
                provider.RemoveAll(cacheKeys);
            }
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public async Task RemoveAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            var tasks=new List<Task>();

            foreach (var provider in _providers)
            {
                tasks.Add(provider.RemoveAsync(cacheKey));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            var tasks = new List<Task>();

            foreach (var provider in _providers)
            {
                tasks.Add(provider.RemoveByPrefixAsync(prefix));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public async Task RemoveAllAsync(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));

            var tasks = new List<Task>();

            foreach (var provider in _providers)
            {
                tasks.Add(provider.RemoveAllAsync(cacheKeys));
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

            var flag = false;

            foreach (var provider in _providers)
            {
                flag = provider.Exists(cacheKey);
                if (flag)
                {
                    break;
                }
            }

            return flag;
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            var flag = false;

            foreach (var provider in _providers)
            {
                flag = await provider.ExistsAsync(cacheKey);
                if (flag)
                {
                    break;
                }
            }

            return await Task.FromResult(flag);
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
            var list=new List<int>();

            foreach (var provider in _providers)
            {
                list.Add(provider.GetCount(prefix));
            }

            return list.OrderByDescending(x => x).FirstOrDefault();
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Flush()
        {
            foreach (var provider in _providers)
            {
                provider.Flush();
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task FlushAsync()
        {
            var tasks=new List<Task>();

            foreach (var provider in _providers)
            {
                tasks.Add(provider.FlushAsync());
            }

            await Task.WhenAll(tasks);
        }
    }
}
