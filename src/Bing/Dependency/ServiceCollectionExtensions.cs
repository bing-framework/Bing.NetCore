using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dependency
{
    /// <summary>
    /// 服务集合(<see cref="IServiceCollection"/>) 扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 获取单例注册服务对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <returns></returns>
        public static T GetSingletonInstanceOrNull<T>(this IServiceCollection services)
        {
            return (T)services.FirstOrDefault(d => d.ServiceType == typeof(T))?.ImplementationInstance;
        }
    }
}
