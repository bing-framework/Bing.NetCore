using System.Reflection;
using Bing.AspNetCore.Mvc;
using Bing.AspNetCore.Uploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore
{
    /// <summary>
    /// 服务扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册上传服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddUploadService(this IServiceCollection services)
        {
            services.AddUploadService<DefaultFileUploadService>();
        }

        /// <summary>
        /// 注册上传服务
        /// </summary>
        /// <typeparam name="TFileUploadService">文件上传服务类型</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddUploadService<TFileUploadService>(this IServiceCollection services)
            where TFileUploadService : class, IFileUploadService
        {
            services.TryAddScoped<IFileUploadService, TFileUploadService>();
        }

        /// <summary>
        /// 注册Api接口服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public static void AddApiInterfaceService(this IServiceCollection services)
        {
            services.AddApiInterfaceService<DefaultApiInterfaceService>();
        }

        /// <summary>
        /// 注册Api接口服务
        /// </summary>
        /// <typeparam name="TApiInterfaceService">Api接口服务类型</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddApiInterfaceService<TApiInterfaceService>(this IServiceCollection services)
            where TApiInterfaceService : class, IApiInterfaceService
        {
            services.TryAddSingleton<IApiInterfaceService, TApiInterfaceService>();
        }

        public static void AddConfigType(this IServiceCollection services, Assembly assembly)
        {

        }
    }
}
