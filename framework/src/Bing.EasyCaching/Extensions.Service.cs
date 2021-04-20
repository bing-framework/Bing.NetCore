using System;
using Bing.Caching;
using EasyCaching.Core.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.EasyCaching
{
    /// <summary>
    /// EasyCaching缓存扩展
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 注册EasyCaching缓存操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">配置操作</param>
        public static IServiceCollection AddCaching(this IServiceCollection services, Action<EasyCachingOptions> configAction)
        {
            services.TryAddScoped<ICache, CacheManager>();
            services.AddEasyCaching(configAction);
            return services;
        }
    }
}
