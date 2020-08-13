using System;
using Bing.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bing.Core
{
    /// <summary>
    /// 服务扩展
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// 获取Bing框架配置选项信息
        /// </summary>
        /// <param name="provider">服务提供程序</param>
        public static BingOptions GetBingOptions(this IServiceProvider provider) => provider.GetService<IOptions<BingOptions>>()?.Value;
    }
}
