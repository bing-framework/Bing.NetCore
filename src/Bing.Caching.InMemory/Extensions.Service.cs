using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            services.TryAddSingleton<ICacheProvider, DefaultInMemoryCacheProvider>();
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
            services.TryAddSingleton<ICacheProvider, DefaultInMemoryCacheProvider>();
        }
    }
}
