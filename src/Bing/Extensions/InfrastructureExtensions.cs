using System;
using Bing.Dependency;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace
namespace Bing
{
    /// <summary>
    /// 基础设施扩展
    /// </summary>
    public static partial class InfrastructureExtensions
    {
        /// <summary>
        /// 注册Bing基础设施服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configs">依赖配置</param>
        /// <returns></returns>
        public static IServiceProvider AddBing(this IServiceCollection services, params IConfig[] configs)
        {
            services.AddHttpContextAccessor();
            return new DependencyConfiguration(services, configs).Config();
        }

        /// <summary>
        /// 注册默认Http上下文
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns></returns>
        public static IServiceCollection AddHttpContextAccessor(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }
            services.TryAddSingleton<IHttpContextAccessor,HttpContextAccessor>();
            return services;
        }
    }
}
