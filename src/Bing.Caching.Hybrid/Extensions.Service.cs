using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Caching.Hybrid
{
    /// <summary>
    /// 缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册默认混合缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddDefaultHybridCache(this IServiceCollection services)
        {
            services.TryAddSingleton<IHybridCacheProvider, DefaultHybridCacheProvider>();
        }
    }
}
