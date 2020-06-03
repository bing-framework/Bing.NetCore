using System;
using System.Linq;
using Bing.Core.Modularity;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// 服务提供程序(<see cref="IServiceProvider"/>) 扩展
    /// </summary>
    public static class BingServiceProviderExtensions
    {
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
            serviceProvider.GetServices<BingModule>().OrderBy(m => m.Level).ThenBy(m => m.Order)
                .ThenBy(m => m.GetType().FullName).ToArray();
    }
}
