namespace Bing.DependencyInjection;

/// <summary>
/// 缓存服务提供程序基类
/// </summary>
public interface ICachedServiceProviderBase// : IServiceProvider
{
    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="defaultValue">默认服务</param>
    T GetService<T>(T defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="defaultValue">默认服务</param>
    object GetService(Type serviceType, object defaultValue);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <param name="factory">服务实例工厂</param>
    T GetService<T>(Func<IServiceProvider, object> factory);

    /// <summary>
    /// 获取服务
    /// </summary>
    /// <param name="serviceType">服务类型</param>
    /// <param name="factory">服务实例工厂</param>
    object GetService(Type serviceType, Func<IServiceProvider, object> factory);
}
