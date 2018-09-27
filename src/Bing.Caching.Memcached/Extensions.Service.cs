using System;
using Bing.Caching.Default;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Bing.Caching.Memcached
{
    /// <summary>
    /// 缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册Memcached缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="providerAction">提供程序操作</param>
        public static void AddDefaultMemcachedCache(this IServiceCollection services,
            Action<MemcachedOptions> providerAction)
        {
            services.AddOptions();
            services.Configure(providerAction);

            services.TryAddTransient<IMemcachedClientConfiguration, DefaultMemcachedClientConfiguration>();
            services.TryAddSingleton<MemcachedClient, MemcachedClient>();
            services.TryAddSingleton<IMemcachedClient>(factory => factory.GetService<MemcachedClient>());

            services.TryAddSingleton<ITranscoder, DefaultMemcachedTranscoder>();
            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.TryAddSingleton<IMemcachedKeyTransformer, DefaultKeyTransformer>();

            services.TryAddSingleton<ICacheProvider, DefaultMemcachedCacheProvider>();
        }

        /// <summary>
        /// 注册Memcached缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        public static void AddDefaultMemcachedCache(this IServiceCollection services, IConfiguration configuration)
        {
            var cacheConfig = configuration.GetSection(CacheConst.MemcachedSection);
            services.Configure<MemcachedOptions>(cacheConfig);

            services.TryAddTransient<IMemcachedClientConfiguration, DefaultMemcachedClientConfiguration>();
            services.TryAddSingleton<MemcachedClient, MemcachedClient>();
            services.TryAddSingleton<IMemcachedClient>(factory => factory.GetService<MemcachedClient>());

            services.TryAddSingleton<ITranscoder, DefaultMemcachedTranscoder>();
            services.TryAddSingleton<ICacheSerializer, DefaultBinaryFormatterSerializer>();
            services.TryAddSingleton<IMemcachedKeyTransformer, DefaultKeyTransformer>();

            services.TryAddSingleton<ICacheProvider, DefaultMemcachedCacheProvider>();
        }

        /// <summary>
        /// 使用默认Memcached缓存
        /// </summary>
        /// <param name="app">App</param>
        public static void UseDefaultMemcached(this IApplicationBuilder app)
        {
            try
            {
                app.ApplicationServices.GetService<IMemcachedClient>().GetAsync<string>("EnyimMemcached").Wait();
                Console.WriteLine("EnyimMemcached Started.");
            }
            catch (Exception ex)
            {
                app.ApplicationServices.GetService<ILogger<IMemcachedClient>>()
                    .LogError(new EventId(), ex, "EnyimMemcached Failed.");
            }
        }
    }
}
