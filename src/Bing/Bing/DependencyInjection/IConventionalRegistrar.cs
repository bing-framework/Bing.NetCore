using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// 通用注册器
    /// </summary>
    public interface IConventionalRegistrar
    {
        /// <summary>
        /// 添加程序集
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assembly">程序集</param>
        void AddAssembly(IServiceCollection services, Assembly assembly);

        /// <summary>
        /// 添加类型数组
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="types">类型数组</param>
        void AddTypes(IServiceCollection services, params Type[] types);

        /// <summary>
        /// 添加类型
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="type">类型</param>
        void AddType(IServiceCollection services, Type type);
    }
}
