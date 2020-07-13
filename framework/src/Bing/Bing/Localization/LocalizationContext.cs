using System;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Bing.Localization
{
    /// <summary>
    /// 本地化上下文
    /// </summary>
    public class LocalizationContext : IServiceProviderAccessor
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 本地化工厂
        /// </summary>
        public IStringLocalizerFactory LocalizerFactory { get; }

        /// <summary>
        /// 初始化一个<see cref="LocalizationContext"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public LocalizationContext(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            LocalizerFactory = ServiceProvider.GetRequiredService<IStringLocalizerFactory>();
        }
    }
}
