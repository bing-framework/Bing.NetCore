using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Caching.Redis
{
    /// <summary>
    /// 缓存扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册 Default Redis 缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        /// <param name="useHybridMode">是否启用混合模式</param>
        public static void AddDefaultRedisCache(this IServiceCollection services, IConfiguration configuration,
            bool useHybridMode = false)
        {
            services.Configure<RedisCacheOptions>(options =>
            {
                RedisBootstrap.SetRedisCacheOptions(configuration, options);
            });

            if (useHybridMode)
            {
                services.AddSingleton<IRedisCacheManager, DefaultRedisCacheManager>();
            }
            else
            {
                services.AddSingleton<ICacheManager, DefaultRedisCacheManager>();
            }
        }
    }
}
