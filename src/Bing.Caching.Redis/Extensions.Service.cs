using System;
using Bing.Caching.Default;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// 缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Redis缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="providerAction">提供程序操作</param>
        public static void AddDefaultRedisCache(this IServiceCollection services, Action<RedisOptions> providerAction)
        {
            services.AddOptions();
            services.Configure(providerAction);

            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.TryAddSingleton<IRedisDatabaseProvider, RedisDatabaseProvider>();
            services.TryAddSingleton<ICacheProvider, DefaultRedisCacheProvider>();
        }

        /// <summary>
        /// 注册Redis缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        public static void AddDefaultRedisCache(this IServiceCollection services, IConfiguration configuration)
        {
            var cacheConfig = configuration.GetSection(CacheConst.RedisSection);
            services.Configure<RedisOptions>(cacheConfig);

            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.TryAddSingleton<IRedisDatabaseProvider, RedisDatabaseProvider>();
            services.TryAddSingleton<ICacheProvider, DefaultRedisCacheProvider>();
        }
    }
}
