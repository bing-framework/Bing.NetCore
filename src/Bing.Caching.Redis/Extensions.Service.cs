using System;
using Bing.Caching.Default;
using Bing.Logs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
            services.AddSingleton<ICacheProvider, DefaultRedisCacheProvider>();
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
            services.AddSingleton<ICacheProvider, DefaultRedisCacheProvider>();
        }

        /// <summary>
        /// 注册Redis缓存操作。使用缓存工厂，实现混合缓存
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="name">名称</param>
        /// <param name="providerAction">提供程序操作</param>
        public static void AddDefaultRedisCacheWithFactory(this IServiceCollection services, string name,
            Action<RedisOptions> providerAction)
        {
            services.AddOptions();
            services.Configure(name, providerAction);

            services.AddSingleton<ICacheProiderFactory, DefaultCacheProviderFactory>();
            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.AddSingleton<IRedisDatabaseProvider, RedisDatabaseProvider>(x =>
            {
                var optionsMon = x.GetRequiredService<IOptionsMonitor<RedisOptions>>();
                var options = optionsMon.Get(name);
                return new RedisDatabaseProvider(name, options);
            });

            services.AddSingleton<ICacheProvider, DefaultRedisCacheProvider>(x =>
            {
                var dbProviders = x.GetServices<IRedisDatabaseProvider>();
                var serializer = x.GetRequiredService<ICacheSerializer>();
                var options = x.GetRequiredService<IOptionsMonitor<RedisOptions>>();
                var log = x.GetService<ILog>();
                return new DefaultRedisCacheProvider(name, dbProviders, serializer, options, log);
            });
        }

        /// <summary>
        /// 注册Redis缓存操作。使用缓存工厂，实现混合缓存
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="name">名称</param>
        /// <param name="configuration">配置</param>
        public static void AddDefaultRedisCacheWithFactory(this IServiceCollection services, string name,
            IConfiguration configuration)
        {
            var cacheConfig = configuration.GetSection(CacheConst.RedisSection);
            services.Configure<RedisOptions>(name, cacheConfig);

            services.AddSingleton<ICacheProiderFactory, DefaultCacheProviderFactory>();
            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.AddSingleton<IRedisDatabaseProvider, RedisDatabaseProvider>(x =>
            {
                var optionsMon = x.GetRequiredService<IOptionsMonitor<RedisOptions>>();
                var options = optionsMon.Get(name);
                return new RedisDatabaseProvider(name, options);
            });

            services.AddSingleton<ICacheProvider, DefaultRedisCacheProvider>(x =>
            {
                var dbProviders = x.GetServices<IRedisDatabaseProvider>();
                var serializer = x.GetRequiredService<ICacheSerializer>();
                var options = x.GetRequiredService<IOptionsMonitor<RedisOptions>>();
                var log = x.GetService<ILog>();
                return new DefaultRedisCacheProvider(name, dbProviders, serializer, options, log);
            });
        }
    }
}
