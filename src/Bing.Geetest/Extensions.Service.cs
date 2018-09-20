using System;
using Bing.Geetest.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Geetest
{
    /// <summary>
    /// 极验扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册极验验证
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="setupAction">配置操作</param>
        public static void AddGeetest(this IServiceCollection services, Action<GeetestConfig> setupAction)
        {
            var config = new GeetestConfig();
            setupAction?.Invoke(config);
            services.AddHttpClient();
            services.TryAddSingleton<IGeetestConfigProvider>(new DefaultGeetestConfigProvider(config));
            services.TryAddScoped<IGeetestManager, GeetestManager>();
        }

        /// <summary>
        /// 注册极验验证
        /// </summary>
        /// <typeparam name="TGeetestConfigProvider">极验配置提供器</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddGeetest<TGeetestConfigProvider>(this IServiceCollection services)
            where TGeetestConfigProvider : class, IGeetestConfigProvider
        {
            services.AddHttpClient();
            services.TryAddScoped<IGeetestConfigProvider, TGeetestConfigProvider>();
            services.TryAddScoped<IGeetestManager,GeetestManager>();
        }
    }
}
