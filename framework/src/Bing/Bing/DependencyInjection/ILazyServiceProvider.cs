using System;

namespace Bing.DependencyInjection
{
    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    public interface ILazyServiceProvider
    {
        /// <summary>
        /// 获取请求服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        T LazyGetRequiredService<T>();

        /// <summary>
        /// 获取请求服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        object LazyGetRequiredService(Type serviceType);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        T LazyGetService<T>();

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        object LazyGetService(Type serviceType);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="defaultValue">默认服务</param>
        T LazyGetService<T>(T defaultValue);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="defaultValue">默认服务</param>
        object LazyGetService(Type serviceType, object defaultValue);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <param name="factory">服务实例工厂</param>
        object LazyGetService(Type serviceType, Func<IServiceProvider, object> factory);

        /// <summary>
        /// 获取服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <param name="factory">服务实例工厂</param>
        T LazyGetService<T>(Func<IServiceProvider, object> factory);
    }
}
