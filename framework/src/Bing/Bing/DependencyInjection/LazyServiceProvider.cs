using System;
using System.Collections.Generic;
using Bing.Collections;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    public class LazyServiceProvider : ILazyServiceProvider, ITransientDependency
    {
        /// <summary>
        /// 缓存服务字典
        /// </summary>
        protected Dictionary<Type, object> CachedServices { get; set; }

        /// <summary>
        /// 服务提供程序
        /// </summary>
        protected IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 初始化一个<see cref="LazyServiceProvider"/>类型的实例
        /// </summary>
        /// <param name="serviceProvider">服务提供程序</param>
        public LazyServiceProvider(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            CachedServices = new Dictionary<Type, object>();
        }

        /// <summary>
        /// 获取请求服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public virtual T LazyGetRequiredService<T>() => (T)LazyGetRequiredService(typeof(T));

        /// <summary>
        /// 获取请求服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public virtual object LazyGetRequiredService(Type serviceType) => CachedServices.GetValueOrAdd(serviceType, _ => ServiceProvider.GetRequiredService(serviceType));

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        public virtual T LazyGetService<T>() => (T)LazyGetService(typeof(T));

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        public virtual object LazyGetService(Type serviceType) => CachedServices.GetValueOrAdd(serviceType, _ => ServiceProvider.GetService(serviceType));

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="defaultValue">默认服务</param>
        public virtual T LazyGetService<T>(T defaultValue) => (T)LazyGetService(typeof(T), defaultValue);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="defaultValue">默认服务</param>
        public virtual object LazyGetService(Type serviceType, object defaultValue) => LazyGetService(serviceType) ?? defaultValue;

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="factory">服务实例工厂</param>
        public virtual object LazyGetService(Type serviceType, Func<IServiceProvider, object> factory) => CachedServices.GetValueOrAdd(serviceType, _ => factory(ServiceProvider));

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="factory">服务实例工厂</param>
        public virtual T LazyGetService<T>(Func<IServiceProvider, object> factory) => (T)LazyGetService(typeof(T), factory);
    }
}
