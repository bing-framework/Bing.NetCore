using System;
using System.Diagnostics;
using System.Linq;
using Bing.Core.Modularity;
using Bing.Reflection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// 服务提供程序(<see cref="IServiceProvider"/>) 扩展
    /// </summary>
    public static partial class BingServiceProviderExtensions
    {
        /// <summary>
        /// 框架初始化
        /// </summary>
        private const string FrameworkLog = "BingFrameworkLog";

        /// <summary>
        /// 根据指定名称开头获取服务
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="name">名称</param>
        public static T GetStartWith<T>(this IServiceProvider serviceProvider, string name) where T : class
        {
            var services = serviceProvider.GetServices<T>();
            return services.FirstOrDefault(m => m.GetType().Name.ToString().StartsWith(name));
        }

        /// <summary>
        /// 根据指定名称结尾获取服务
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="name">名称</param>
        public static T GetEndsWith<T>(this IServiceProvider serviceProvider, string name) where T : class
        {
            var services = serviceProvider.GetServices<T>();
            return services.FirstOrDefault(m => m.GetType().Name.ToString().EndsWith(name));
        }

        /// <summary>
        /// 获取所有模块信息
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public static BingModule[] GetAllModules(this IServiceProvider serviceProvider) =>
            serviceProvider.GetServices<BingModule>()
                .OrderBy(m => m.Level)
                .ThenBy(m => m.Order)
                .ThenBy(m => m.GetType().FullName)
                .ToArray();

        /// <summary>
        /// Bing模块初始化，适用于非AspNetCore环境
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public static IServiceProvider UseBing(this IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetLogger(FrameworkLog);
            logger.LogInformation("Bing框架初始化开始");
            var watch = Stopwatch.StartNew();
            var modules = serviceProvider.GetServices<BingModule>().ToArray();
            foreach (var module in modules)
            {
                var moduleName = Reflections.GetDescription(module.GetType());
                logger.LogInformation($"正在初始化模块 “{moduleName}”");
                module.UseModule(serviceProvider);
                logger.LogInformation($"模块 “{moduleName}” 初始化完成");
            }
            watch.Stop();
            logger.LogInformation($"Bing框架初始化完毕，耗时：{watch.Elapsed}");
            return serviceProvider;
        }

        /// <summary>
        /// 获取指定类型的日志对象
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="serviceProvider">服务提供程序</param>
        public static ILogger<T> GetLogger<T>(this IServiceProvider serviceProvider)
        {
            var factory = serviceProvider.GetService<ILoggerFactory>();
            return factory.CreateLogger<T>();
        }

        /// <summary>
        /// 获取指定类型的日志对象
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="type">类型</param>
        public static ILogger GetLogger(this IServiceProvider serviceProvider, Type type)
        {
            var factory = serviceProvider.GetService<ILoggerFactory>();
            return factory.CreateLogger(type);
        }

        /// <summary>
        /// 获取指定名称的日志对象
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        /// <param name="name">名称</param>
        public static ILogger GetLogger(this IServiceProvider serviceProvider, string name)
        {
            var factory = serviceProvider.GetService<ILoggerFactory>();
            return factory.CreateLogger(name);
        }
    }
}
