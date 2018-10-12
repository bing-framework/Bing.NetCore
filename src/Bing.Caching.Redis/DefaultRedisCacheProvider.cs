using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bing.Caching.CacheStats;
using Bing.Logs;
using Bing.Logs.Core;
using Bing.Utils.Helpers;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// 默认Redis缓存提供程序
    /// </summary>
    public class DefaultRedisCacheProvider:ICacheProvider
    {
        /// <summary>
        /// 缓存数据库
        /// </summary>
        private readonly IDatabase _cache;

        /// <summary>
        /// 服务器
        /// </summary>
        private readonly IEnumerable<IServer> _servers;

        /// <summary>
        /// Redis数据库提供程序
        /// </summary>
        private readonly IRedisDatabaseProvider _dbProvider;

        /// <summary>
        /// 缓存序列化器
        /// </summary>
        private readonly ICacheSerializer _serializer;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// Redis选项
        /// </summary>
        private readonly RedisOptions _options;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; }

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
        public CacheStatsInfo CacheStatsInfo { get; }

        /// <summary>
        /// 初始化一个<see cref="DefaultRedisCacheProvider"/>类型的实例
        /// </summary>
        /// <param name="dbProvider">Redis数据库提供程序</param>
        /// <param name="serializer">缓存序列化器</param>
        /// <param name="options">Redis选项</param>
        /// <param name="log">日志</param>
        public DefaultRedisCacheProvider(IRedisDatabaseProvider dbProvider, ICacheSerializer serializer,
            IOptionsMonitor<RedisOptions> options, ILog log = null)
        {
            Check.NotNull(dbProvider,nameof(dbProvider));
            Check.NotNull(serializer,nameof(serializer));

            this._dbProvider = dbProvider;
            this._serializer = serializer;
            this._options = options.CurrentValue;
            this._log = log ?? NullLog.Instance;
            this._cache = _dbProvider.GetDatabase();
            this._servers = _dbProvider.GetServerList();
            this.CacheStatsInfo = new CacheStatsInfo();
        }

        /// <summary>
        /// 初始化一个<see cref="DefaultRedisCacheProvider"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="dbProviders">Redis数据库提供程序列表</param>
        /// <param name="serializer">缓存序列化器</param>
        /// <param name="options">Redis选项</param>
        /// <param name="log">日志</param>
        public DefaultRedisCacheProvider(string name,IEnumerable<IRedisDatabaseProvider> dbProviders, ICacheSerializer serializer,
            IOptionsMonitor<RedisOptions> options, ILog log = null)
        {
            Check.NotNullOrEmpty(dbProviders, nameof(dbProviders));
            Check.NotNull(serializer, nameof(serializer));

            this._dbProvider = dbProviders.FirstOrDefault(x => x.DbProviderName.Equals(name));
            this._serializer = serializer;
            this._options = options.CurrentValue;
            this._log = log ?? NullLog.Instance;
            this._cache = _dbProvider.GetDatabase();
            this._servers = _dbProvider.GetServerList();
            this.CacheStatsInfo = new CacheStatsInfo();

            this.Name = name;
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

            _cache.StringSet(cacheKey, _serializer.Serialize(cacheValue), expiry);
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

            var batch = _cache.CreateBatch();
            foreach (var value in values)
            {
                batch.StringSetAsync(value.Key, _serializer.Serialize(value.Value), expiry);
            }
            batch.Execute();
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

            await _cache.StringSetAsync(cacheKey, _serializer.Serialize(cacheValue), expiry);
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
                tasks.Add(SetAsync(value.Key,value.Value,expiry));
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

            var result = _cache.StringGet(cacheKey);

            if (!result.IsNull)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
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

            var result = _cache.StringGet(cacheKey);
            if (!result.IsNull)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            var item = dataRetriever?.Invoke();
            if (item != null)
            {
                Set(cacheKey, item, expiry);
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

            var keyArray = cacheKeys.ToArray();
            var values = _cache.StringGet(keyArray.Select(x => (RedisKey) x).ToArray());

            var result = new Dictionary<string, CacheValue<T>>();
            for (var i = 0; i < keyArray.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                {
                    result.Add(keyArray[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                }
                else
                {
                    result.Add(keyArray[i], CacheValue<T>.NoValue);
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
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            prefix = this.HandlerPrefix(prefix);
            WriteLog($"GetByPrefix : prefix = {prefix}");

            var redisKeys = this.SearchRedisKeys(prefix);

            var values = _cache.StringGet(redisKeys).ToArray();

            var result = new Dictionary<string, CacheValue<T>>();

            for (var i = 0; i < redisKeys.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                {
                    result.Add(redisKeys[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                }
                else
                {
                    result.Add(redisKeys[i], CacheValue<T>.NoValue);
                }
            }

            return result;
        }

        /// <summary>
        /// 处理缓存键前缀
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <returns></returns>
        private string HandlerPrefix(string prefix)
        {
            // 禁止
            if (prefix.Trim().Equals("*"))
            {
                throw new ArgumentException("前缀不能等于 * ");
            }
            // 禁止前缀开头为 *
            prefix = new Regex("^\\*+").Replace(prefix, "");

            // 尾部匹配 *
            if (!prefix.EndsWith("*", StringComparison.OrdinalIgnoreCase))
            {
                prefix = string.Concat(prefix, "*");
            }

            return prefix;
        }

        /// <summary>
        /// 查询Redis缓存键
        /// </summary>
        /// <param name="pattern">前缀</param>
        /// <remarks>
        /// 如果您的Redis服务器支持命令 SCAN，
        /// IServer.Keys 将使用命令SCAN找出密钥，
        /// 以下：
        /// https://github.com/StackExchange/StackExchange.Redis/blob/master/StackExchange.Redis/StackExchange/Redis/RedisServer.cs#L289
        /// </remarks>
        /// <returns></returns>
        private RedisKey[] SearchRedisKeys(string pattern)
        {
            var keys=new List<RedisKey>();

            foreach (var server in _servers)
            {
                keys.AddRange(server.Keys(pattern: pattern, database: _cache.Database));
            }
            return keys.Distinct().ToArray();

            //var keys = new HashSet<RedisKey>();

            //int nextCursor = 0;
            //do
            //{
            //    RedisResult redisResult = _cache.Execute("SCAN", nextCursor.ToString(), "MATCH", pattern, "COUNT", "1000");
            //    var innerResult = (RedisResult[])redisResult;

            //    nextCursor = int.Parse((string)innerResult[0]);

            //    List<RedisKey> resultLines = ((RedisKey[])innerResult[1]).ToList();

            //    keys.UnionWith(resultLines);
            //}
            //while (nextCursor != 0);

            //return keys.ToArray();
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

            var result =await _cache.StringGetAsync(cacheKey);

            if (!result.IsNull)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
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

            var result = await _cache.StringGetAsync(cacheKey);
            if (!result.IsNull)
            {
                WriteLog($"缓存击中 : cacheKey = {cacheKey}");
                CacheStatsInfo.OnHit();
                var value = _serializer.Deserialize<T>(result);
                return new CacheValue<T>(value, true);
            }

            CacheStatsInfo.OnMiss();
            WriteLog($"缓存穿透 : cacheKey = {cacheKey}");

            var item = await dataRetriever?.Invoke();
            if (item != null)
            {
                await SetAsync(cacheKey, item, expiry);
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

            var keyArray = cacheKeys.ToArray();
            var values = await _cache.StringGetAsync(keyArray.Select(x => (RedisKey)x).ToArray());

            var result = new Dictionary<string, CacheValue<T>>();
            for (var i = 0; i < keyArray.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                {
                    result.Add(keyArray[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                }
                else
                {
                    result.Add(keyArray[i], CacheValue<T>.NoValue);
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
        public async Task<IDictionary<string, CacheValue<T>>> GetByPrefixAsync<T>(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            prefix = this.HandlerPrefix(prefix);
            WriteLog($"GetByPrefixAsync : prefix = {prefix}");

            var redisKeys = this.SearchRedisKeys(prefix);

            var values = (await _cache.StringGetAsync(redisKeys)).ToArray();

            var result = new Dictionary<string, CacheValue<T>>();

            for (var i = 0; i < redisKeys.Length; i++)
            {
                var cachedValue = values[i];
                if (!cachedValue.IsNull)
                {
                    result.Add(redisKeys[i], new CacheValue<T>(_serializer.Deserialize<T>(cachedValue), true));
                }
                else
                {
                    result.Add(redisKeys[i], CacheValue<T>.NoValue);
                }
            }

            return result;
        }

        /// <summary>
        /// 移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        public void Remove(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));
            WriteLog($"Remove : cacheKey = {cacheKey}");

            _cache.KeyDelete(cacheKey);
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public void RemoveByPrefix(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            prefix = this.HandlerPrefix(prefix);
            WriteLog($"RemoveByPrefix : prefix = {prefix}");

            var redisKeys = this.SearchRedisKeys(prefix);

            _cache.KeyDelete(redisKeys);
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public void RemoveAll(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"RemoveAll : cacheKeys = {string.Join(",", cacheKeys)}");

            var redisKeys = cacheKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => (RedisKey) x).ToArray();
            if (redisKeys.Length > 0)
            {
                _cache.KeyDelete(redisKeys);
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

            await _cache.KeyDeleteAsync(cacheKey);
        }

        /// <summary>
        /// 移除缓存，根据缓存键前缀
        /// </summary>
        /// <param name="prefix">缓存键前缀</param>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            Check.NotNullOrEmpty(prefix, nameof(prefix));

            prefix = this.HandlerPrefix(prefix);
            WriteLog($"RemoveByPrefixAsync : prefix = {prefix}");

            var redisKeys = this.SearchRedisKeys(prefix);

            await _cache.KeyDeleteAsync(redisKeys);
        }

        /// <summary>
        /// 批量移除缓存，根据缓存键
        /// </summary>
        /// <param name="cacheKeys">缓存键集合</param>
        public async Task RemoveAllAsync(IEnumerable<string> cacheKeys)
        {
            Check.NotNullOrEmpty(cacheKeys, nameof(cacheKeys));
            WriteLog($"RemoveAllAsync : cacheKeys = {string.Join(",", cacheKeys)}");

            var redisKeys = cacheKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => (RedisKey)x).ToArray();
            if (redisKeys.Length > 0)
            {
                await _cache.KeyDeleteAsync(redisKeys);
            }
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public bool Exists(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            return _cache.KeyExists(cacheKey);
        }

        /// <summary>
        /// 是否存在指定缓存
        /// </summary>
        /// <param name="cacheKey">缓存键</param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(string cacheKey)
        {
            Check.NotNullOrEmpty(cacheKey, nameof(cacheKey));

            return await _cache.KeyExistsAsync(cacheKey);
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
            if (string.IsNullOrWhiteSpace(prefix))
            {
                var allCount = 0;
                foreach (var server in _servers)
                {
                    allCount += (int) server.DatabaseSize(_cache.Database);
                }

                return allCount;
            }

            return this.SearchRedisKeys(this.HandlerPrefix(prefix)).Length;
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public void Flush()
        {
            WriteLog($"Flush");

            foreach (var server in _servers)
            {
                server.FlushDatabase(_cache.Database);
            }
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public async Task FlushAsync()
        {
            WriteLog($"FlushAsync");

            var tasks = new List<Task>();

            foreach (var server in _servers)
            {
                tasks.Add(server.FlushDatabaseAsync(_cache.Database));
            }

            await Task.WhenAll(tasks);
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

        /// <summary>
        /// 构建缓存键
        /// </summary>
        /// <param name="providerName">提供程序名称</param>
        /// <param name="cahceKey">缓存键</param>
        /// <returns></returns>
        private string BuildCacheKey(string providerName, string cahceKey)
        {
            return string.IsNullOrWhiteSpace(providerName) ? cahceKey : $"{providerName}-{cahceKey}";
        }
    }
}
