using Bing.AspNetCore.Mvc;
using Bing.AspNetCore.Uploads;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore.Extensions
{
    /// <summary>
    /// 服务集合(<see cref="IServiceCollection"/>)扩展
    /// </summary>
    public static class BingServiceCollectionExtensions
    {
        /// <summary>
        /// 注册上传服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddUploadService(this IServiceCollection services) => services.AddUploadService<DefaultFileUploadService>();

        /// <summary>
        /// 注册上传服务
        /// </summary>
        /// <typeparam name="TFileUploadService">文件上传服务类型</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddUploadService<TFileUploadService>(this IServiceCollection services) where TFileUploadService : class, IFileUploadService => services.TryAddScoped<IFileUploadService, TFileUploadService>();

        /// <summary>
        /// 注册Api接口服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddApiInterfaceService(this IServiceCollection services) => services.AddApiInterfaceService<DefaultApiInterfaceService>();

        /// <summary>
        /// 注册Api接口服务
        /// </summary>
        /// <typeparam name="TApiInterfaceService">Api接口服务类型</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddApiInterfaceService<TApiInterfaceService>(this IServiceCollection services) where TApiInterfaceService : class, IApiInterfaceService =>
            services.TryAddSingleton<IApiInterfaceService, TApiInterfaceService>();

        /// <summary>
        /// 获取<see cref="IHostingEnvironment"/>环境信息
        /// </summary>
        /// <param name="services">服务集合</param>
        public static IHostingEnvironment GetHostingEnvironment(this IServiceCollection services) => services.GetSingletonInstance<IHostingEnvironment>();
    }
}
