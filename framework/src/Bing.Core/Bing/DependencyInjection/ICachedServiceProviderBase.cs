namespace Bing.DependencyInjection;

/// <summary>
/// 定义按服务类型缓存解析结果的服务提供程序能力。
/// </summary>
public interface ICachedServiceProviderBase// : IServiceProvider
{
    /// <summary>
    /// 获取指定服务；未解析到时返回默认值。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="defaultValue">服务未注册或解析结果为 <c>null</c> 时返回的默认值。</param>
    /// <returns>缓存的服务实例或 <paramref name="defaultValue"/>。</returns>
    T GetService<T>(T defaultValue);

    /// <summary>
    /// 获取指定类型的服务；未解析到时返回默认值。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <param name="defaultValue">服务未注册或解析结果为 <c>null</c> 时返回的默认值。</param>
    /// <returns>缓存的服务实例或 <paramref name="defaultValue"/>。</returns>
    object GetService(Type serviceType, object defaultValue);

    /// <summary>
    /// 获取指定服务；首次按该服务类型解析时由工厂创建并缓存。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="factory">首次解析时用于创建服务实例的工厂。</param>
    /// <returns>缓存的服务实例。</returns>
    T GetService<T>(Func<IServiceProvider, object> factory);

    /// <summary>
    /// 获取指定类型的服务；首次按该服务类型解析时由工厂创建并缓存。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <param name="factory">首次解析时用于创建服务实例的工厂。</param>
    /// <returns>缓存的服务实例。</returns>
    object GetService(Type serviceType, Func<IServiceProvider, object> factory);
}
