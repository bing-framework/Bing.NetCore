using System;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// AspectCore扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 启用Aop
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">Aop配置</param>
        public static void EnableAop(this IServiceCollection services, Action<IAspectConfiguration> configAction = null)
        {
            services.ConfigureDynamicProxy(config =>
            {
                config.EnableParameterAspect();
                config.NonAspectPredicates.Add(t =>
                    Reflection.GetTopBaseType(t.DeclaringType).SafeString() ==
                    "Microsoft.EntityFrameworkCore.DbContext");
                configAction?.Invoke(config);
            });
            services.EnableAspectScoped();
        }

        /// <summary>
        /// 启用Aop作用域
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void EnableAspectScoped(this IServiceCollection services)
        {
            services.AddScoped<IAspectScheduler, ScopeAspectScheduler>();
            services.AddScoped<IAspectBuilderFactory, ScopeAspectBuilderFactory>();
            services.AddScoped<IAspectContextFactory, ScopeAspectContextFactory>();
        }
    }
}
