using System;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;
using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using Bing.Extensions;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// AspectCore 扩展
    /// </summary>
    public static class AspectCoreExtensions
    {
        /// <summary>
        /// 启用Aop
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configAction">Aop配置</param>
        /// <remarks>
        /// 默认不开启参数拦截 EnableParameterAspect()，该方法将会导致异常不能更好的定位
        /// </remarks>
        public static void EnableAop(this IServiceCollection services, Action<IAspectConfiguration> configAction = null)
        {
            services.ConfigureDynamicProxy(config =>
            {
                //config.EnableParameterAspect();// 启用参数拦截，会导致异常不能很好的定位
                config.NonAspectPredicates.Add(t => Reflections.GetTopBaseType(t.DeclaringType).SafeString() == "Microsoft.EntityFrameworkCore.DbContext");
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
