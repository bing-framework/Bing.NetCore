using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// <see cref="ServiceLifetime.Scoped"/>服务解析器
    /// </summary>
    public interface IScopedServiceResolver
    {
        /// <summary>
        /// 是否可解析
        /// </summary>
        bool ResolveEnabled { get; }

        /// <summary>
        /// <see cref="ServiceLifetime.Scoped"/>生命周期的服务提供程序
        /// </summary>
        IServiceProvider ScopedProvider { get; }

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        T GetService<T>();

        /// <summary>
        /// 获取指定服务类型的实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        object GetService(Type serviceType);

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        IEnumerable<T> GetServices<T>();

        /// <summary>
        /// 获取指定服务类型的所有实例
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        IEnumerable<object> GetServices(Type serviceType);
    }
}
