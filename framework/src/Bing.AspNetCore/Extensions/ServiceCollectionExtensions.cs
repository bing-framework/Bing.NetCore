using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.AspNetCore
{
    /// <summary>
    /// 服务集合(<see cref="IServiceCollection"/>) 扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 获取<see cref="IHostingEnvironment"/>环境信息
        /// </summary>
        /// <param name="services">服务集合</param>
        public static IHostingEnvironment GetHostingEnvironment(this IServiceCollection services) => services.GetSingletonInstance<IHostingEnvironment>();
    }
}
