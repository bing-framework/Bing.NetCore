using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.InMemory
{
    /// <summary>
    /// 内存缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册默认内存缓存
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        /// <param name="useHybridMode">是否启用混合模式</param>
        public static void AddDefaultInMemoryCache(this IServiceCollection services, IConfiguration configuration,
            bool useHybridMode = false)
        {
            services.AddMemoryCache(options => configuration.GetSection("Cache.MemoryCache"));
            if (useHybridMode)
            {
                services.AddSingleton<IInMemoryCacheManager, DefaultInMemoryCacheManager>();
            }
            else
            {
                services.AddSingleton<ICacheManager, DefaultInMemoryCacheManager>();
            }
        }
    }
}
