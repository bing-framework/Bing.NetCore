using System;
using System.Threading;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Utils.Extensions;
using Bing.Utils.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// 默认Redis缓存管理器
    /// </summary>
    public class DefaultRedisCacheManager:IRedisCacheManager
    {
        /// <summary>
        /// 分布式缓存
        /// </summary>
        public CacheLevel CacheLevel => CacheLevel.Distributed;

        /// <summary>
        /// 配置
        /// </summary>
        private readonly RedisCacheOptions _options;

        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// Redis 连接多路复用器
        /// </summary>
        private Lazy<ConnectionMultiplexer> _connectionMultiplexer;

        /// <summary>
        /// Redis 数据库
        /// </summary>
        private IDatabase _database;

        /// <summary>
        /// 单例对象锁
        /// </summary>
        private static readonly object Locker=new object();

        /// <summary>
        /// 是否已经回收
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 互斥密钥格式
        /// </summary>
        private const string MutexKeyFormat = "{0}:mutex";

        /// <summary>
        /// Lua 解锁脚本
        /// </summary>
        private const string UnlockScript = @"
            if redis.call(""get"",KEYS[1]) == ARGV[1] then
                return redis.call(""del"",KEYS[1])
            else
                return 0
            end";

        /// <summary>
        /// 初始化一个<see cref="DefaultRedisCacheManager"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="log">日志</param>
        public DefaultRedisCacheManager(IOptions<RedisCacheOptions> options, ILog log)
        {
            _options = options.Value;
            _log = log;
            _connectionMultiplexer = new Lazy<ConnectionMultiplexer>(CreateConnection, true);
        }

        /// <summary>
        /// 创建连接
        /// </summary>
        /// <returns></returns>
        private ConnectionMultiplexer CreateConnection()
        {
            try
            {
                ConfigurationOptions configurationOptions =
                    ConfigurationOptions.Parse($"{_options.Host}:{_options.Port}");
                if (!_options.Password.IsEmpty())
                {
                    configurationOptions.Password = _options.Password;
                }
                return ConnectionMultiplexer.Connect(configurationOptions);
            }
            catch (Exception e)
            {
                _log.Caption("Redis连接错误");
                e.Log(_log);
                throw;
            }
        }

        /// <summary>
        /// 获取Redis数据库
        /// </summary>
        /// <returns></returns>
        protected IDatabase GetDatabase()
        {
            if (_database == null)
            {
                lock (Locker)
                {
                    if (_database == null)
                    {
                        _database = _connectionMultiplexer.Value.GetDatabase(_options.Db);
                    }
                }
            }

            return _database;
        }

        /// <summary>
        /// 创建唯一锁ID
        /// </summary>
        /// <returns></returns>
        protected static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
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
            var value = GetDatabase().StringGet(CacheKeyConverter.GetKeyWithRegion(key));
            if (value.HasValue)
            {
                return GetValue<T>(value);
            }

            var mutexKey = string.Format(MutexKeyFormat, key);

            // 防击穿（热点key并发）
            var uniqueValue = CreateUniqueLockId();
            try
            {
                if (SetNotExists(mutexKey, uniqueValue, TimeSpan.FromMinutes(1)))
                {
                    var result = getData();
                    _log.Info($"GetOrAdd,[key:{key}], call getData func");

                    // 防穿透
                    if (result == null)
                    {
                        Set(key, default(T), TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        Set(key, result, expiry);
                    }

                    return result;
                }
            }
            finally
            {
                UnlockInStance(mutexKey,uniqueValue);
            }
            Thread.Sleep(50);
            return GetOrAdd<T>(key, getData, expiry);
        }

        /// <summary>
        /// 获取key的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        public T Get<T>(string key)
        {
            return GetValue<T>(GetDatabase().StringGet(CacheKeyConverter.GetKeyWithRegion(key)));
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
            return GetDatabase().StringSet(CacheKeyConverter.GetKeyWithRegion(key), JsonUtil.ToJson(value), expiry);
        }

        /// <summary>
        /// 删除指定key
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns></returns>
        public bool Delete(string key)
        {
            return GetDatabase().KeyDelete(CacheKeyConverter.GetKeyWithRegion(key));
        }

        /// <summary>
        /// 获取缓存结果
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="redisValue">缓存数据</param>
        /// <returns></returns>
        private T GetValue<T>(RedisValue redisValue)
        {
            return redisValue.HasValue ? JsonUtil.ToObject<T>(redisValue) : default(T);
        }

        /// <summary>
        /// 解锁实例
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        private void UnlockInStance(string key, byte[] value)
        {
            RedisKey[] keys = {CacheKeyConverter.GetKeyWithRegion(key)};
            RedisValue[] values = {value};
            GetDatabase().ScriptEvaluate(UnlockScript, keys, values);
        }

        /// <summary>
        /// 设置不存在的值
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="value">值</param>
        /// <param name="expiry">过期时间</param>
        /// <returns></returns>
        private bool SetNotExists(string key, byte[] value, TimeSpan? expiry = null)
        {
            return GetDatabase().StringSet(CacheKeyConverter.GetKeyWithRegion(key), value, expiry, When.NotExists);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 回收
        /// </summary>
        /// <param name="disposing">是否回收内部资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _database = null;
                _connectionMultiplexer?.Value?.Close();
                _connectionMultiplexer?.Value?.Dispose();
                _connectionMultiplexer = null;
            }

            _disposed = true;
        }
    }
}