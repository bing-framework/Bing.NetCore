using Microsoft.AspNetCore.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// 服务集合(<see cref="IServiceCollection"/>) 扩展
    /// </summary>
    public static class BingAspNetCoreServiceCollectionExtensions
    {
#if NETCOREAPP3_0 || NETCOREAPP3_1 || NET5_0
        /// <summary>
        /// 获取<see cref="IWebHostEnvironment"/>环境信息
        /// </summary>
        /// <param name="services">服务集合</param>
        public static IWebHostEnvironment GetWebHostEnvironment(this IServiceCollection services) => services.GetSingletonInstance<IWebHostEnvironment>();
#elif NETSTANDARD2_0
        /// <summary>
        /// 获取<see cref="IHostingEnvironment"/>环境信息
        /// </summary>
        /// <param name="services">服务集合</param>
        public static IHostingEnvironment GetHostingEnvironment(this IServiceCollection services) => services.GetSingletonInstance<IHostingEnvironment>();
#endif
    }
}
