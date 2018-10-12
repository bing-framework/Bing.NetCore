using System;
using Bing.Caching.Default;
using Bing.Logs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册默认内存缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddDefaultInMemoryCache(this IServiceCollection services)
        {
            var option = new InMemoryOptions();
            services.AddDefaultInMemoryCache(x =>
            {
                x.CacheProviderType = option.CacheProviderType;
                x.MaxRdSecond = option.MaxRdSecond;
                x.Order = option.Order;
            });
        }

        /// <summary>
        /// 注册默认内存缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="providerAction">提供程序操作</param>
        public static void AddDefaultInMemoryCache(this IServiceCollection services,
            Action<InMemoryOptions> providerAction)
        {
            services.AddOptions();
            services.Configure(providerAction);

            services.AddMemoryCache();
            services.AddSingleton<ICacheProvider, DefaultInMemoryCacheProvider>();
        }

        /// <summary>
        /// 注册默认内存缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        public static void AddDefaultInMemoryCache(this IServiceCollection services, IConfiguration configuration)
        {
            var dbConfig = configuration.GetSection(CacheConst.InMemorySection);
            services.Configure<InMemoryOptions>(dbConfig);

            services.AddMemoryCache();
            services.AddSingleton<ICacheProvider, DefaultInMemoryCacheProvider>();
        }


        /// <summary>
        /// 注册默认内存缓存操作。使用缓存工厂，实现混合缓存
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="providerName">提供程序名称</param>
        public static void AddDefaultInMemoryCacheWithFactory(this IServiceCollection services,
            string providerName = CacheConst.DefaultInMemoryName)
        {
            var option = new InMemoryOptions();
            services.AddDefaultInMemoryCacheWithFactory(providerName, x =>
            {
                x.CacheProviderType = option.CacheProviderType;
                x.MaxRdSecond = option.MaxRdSecond;
                x.Order = option.Order;
            });
        }

        /// <summary>
        /// 注册默认内存缓存操作。使用缓存工厂，实现混合缓存
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="providerName">提供程序名称</param>
        /// <param name="providerAction">提供程序操作</param>
        public static void AddDefaultInMemoryCacheWithFactory(this IServiceCollection services, string providerName,
            Action<InMemoryOptions> providerAction)
        {
            services.AddOptions();
            services.Configure(providerAction);

            services.AddMemoryCache();

            services.AddSingleton<ICacheProiderFactory, DefaultCacheProviderFactory>();
            services.AddSingleton<ICacheProvider, DefaultInMemoryCacheProvider>(x =>
            {
                var memoryCache = x.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var options = x.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<InMemoryOptions>>();

                var log = x.GetService<ILog>();
                return new DefaultInMemoryCacheProvider(providerName, memoryCache, options, log);
            });
        }
    }
}
