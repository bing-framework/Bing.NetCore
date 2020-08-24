using System;
using System.Linq;
using Bing.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// 服务集合 - 日志 扩展
    /// </summary>
    public static class ServiceCollectionLoggerExtensions
    {
        /// <summary>
        /// 添加服务调试日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="oldDescriptors">旧的服务描述集合</param>
        /// <param name="logName">日志名称</param>
        public static IServiceCollection ServiceLogDebug(this IServiceCollection services, ServiceDescriptor[] oldDescriptors, string logName)
        {
            var list = services.Except(oldDescriptors);
            foreach (var descriptor in list)
            {
                if (descriptor.ImplementationType != null)
                {
                    services.ServiceLogDebug(descriptor.ServiceType, descriptor.ImplementationType, logName, descriptor.Lifetime);
                    continue;
                }
                if (descriptor.ImplementationInstance != null)
                    services.ServiceLogDebug(descriptor.ServiceType, descriptor.ImplementationInstance.GetType(), logName, descriptor.Lifetime);
            }
            return services;
        }

        /// <summary>
        /// 添加服务调试日志
        /// </summary>
        /// <typeparam name="TServiceType">服务类型</typeparam>
        /// <typeparam name="TImplementType">服务实现类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="logName">日志名称</param>
        /// <param name="lifetime">生命周期</param>
        public static IServiceCollection ServiceLogDebug<TServiceType, TImplementType>(this IServiceCollection services, string logName, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            var serviceType = typeof(TServiceType);
            var implementType = typeof(TImplementType);
            return services.ServiceLogDebug(serviceType, implementType, logName, lifetime);
        }

        /// <summary>
        /// 服务调试日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="serviceType">服务类型</param>
        /// <param name="implementType">服务实现类型</param>
        /// <param name="logName">日志名称</param>
        /// <param name="lifetime">生命周期</param>
        public static IServiceCollection ServiceLogDebug(this IServiceCollection services, Type serviceType, Type implementType, string logName, ServiceLifetime lifetime = ServiceLifetime.Singleton)
        {
            var lifetimeType = lifetime == ServiceLifetime.Singleton
                ? "单例"
                : lifetime == ServiceLifetime.Scoped
                    ? "作用域"
                    : "瞬时";
            return services.LogDebug($"添加服务: {lifetimeType}, {serviceType.FullName} -> {implementType.FullName}", logName);
        }

        /// <summary>
        /// 添加启动调试日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="message">消息</param>
        /// <param name="logName">日志名称</param>
        public static IServiceCollection LogDebug(this IServiceCollection services, string message, string logName)
        {
            var logger = services.GetOrAddSingletonInstance(() => new StartupLogger());
            logger.LogDebug(message, logName);
            return services;
        }

        /// <summary>
        /// 添加启动消息日志
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="message">消息</param>
        /// <param name="logName">日志名称</param>
        public static IServiceCollection LogInformation(this IServiceCollection services, string message, string logName)
        {
            var logger = services.GetOrAddSingletonInstance(() => new StartupLogger());
            logger.LogInformation(message, logName);
            return services;
        }
    }
}
