using Bing.Exceptions;
using Bing.Utils.Extensions;
using Microsoft.Extensions.Configuration;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// Redis启动
    /// </summary>
    public static class RedisBootstrap
    {
        /// <summary>
        /// 设置Redis缓存配置
        /// </summary>
        /// <param name="configuration">配置</param>
        /// <param name="options">配置对象</param>
        internal static void SetRedisCacheOptions(IConfiguration configuration, RedisCacheOptions options)
        {
            string host = configuration.GetValue<string>("Cache:Redis:Host");
            if (host.IsEmpty())
            {
                throw new ConfigException("Cache.Redis.Host");
            }

            string port = configuration.GetValue<string>("Cache:Redis:Port");
            if (port.IsEmpty())
            {
                port = "6379";
            }

            string password = configuration.GetValue<string>("Cache:Redis:Password");
            string dbStr = configuration.GetValue<string>("Cache:Redis:Db");
            int db = 0;
            if (!dbStr.IsEmpty() && int.TryParse(dbStr, out int result))
            {
                db = result;
            }

            options.Host = host;
            options.Port = port;
            options.Password = password;
            options.Db = db;

            // 设置缓存区域
            CacheKeyConverter.SetRegion(configuration);
        }
    }
}
