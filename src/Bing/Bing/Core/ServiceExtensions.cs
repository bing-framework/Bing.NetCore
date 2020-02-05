using System;
using Bing.Core.Builders;
using Bing.Core.Modularity;
using Bing.Core.Options;
using Bing.Helpers;
using Bing.Reflections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Bing.Core
{
    /// <summary>
    /// 服务扩展
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// 将Bing服务，各个<see cref="BingModule"/>模块的服务添加到服务容器中
        /// </summary>
        /// <typeparam name="TBingModuleManager">Bing模块管理器类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="builderAction">Bing构建器操作</param>
        public static IServiceCollection AddBing<TBingModuleManager>(this IServiceCollection services,
            Action<IBingBuilder> builderAction = null) where TBingModuleManager : IBingModuleManager, new()
        {
            Check.NotNull(services, nameof(services));

            var configuration = services.GetConfiguration();
            Singleton<IConfiguration>.Instance = configuration;

            // 初始化所有程序集查找器
            services.TryAddSingleton<IAllAssemblyFinder>(new AppDomainAllAssemblyFinder());

            var builder = services.GetSingletonInstanceOrNull<IBingBuilder>() ?? new BingBuilder();
            builderAction?.Invoke(builder);
            services.TryAddSingleton<IBingBuilder>(builder);

            var manager = new TBingModuleManager();
            services.AddSingleton<IBingModuleManager>(manager);
            manager.LoadModules(services);
            return services;
        }

        /// <summary>
        /// 获取<see cref="IConfiguration"/>配置信息
        /// </summary>
        /// <param name="services">服务集合</param>
        public static IConfiguration GetConfiguration(this IServiceCollection services) => services.GetSingletonInstanceOrNull<IConfiguration>();

        /// <summary>
        /// 获取Bing框架配置选项信息
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        public static BingOptions GetBingOptions(this IServiceProvider provider) => provider.GetService<IOptions<BingOptions>>()?.Value;

        /// <summary>
        /// Bing框架初始化，适用于非AspNetCore环境
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        public static IServiceProvider UseBing(this IServiceProvider provider)
        {
            var moduleManager = provider.GetService<IBingModuleManager>();
            moduleManager.UseModule(provider);
            return provider;
        }
    }
}
