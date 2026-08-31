namespace Bing.DependencyInjection;

/// <summary>
/// 定义延迟解析服务的提供程序能力。
/// </summary>
public interface ILazyServiceProvider
{
    /// <summary>
    /// 获取必须已注册的服务。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>已注册的服务实例。</returns>
    T LazyGetRequiredService<T>();

    /// <summary>
    /// 获取指定类型且必须已注册的服务。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <returns>已注册的服务实例。</returns>
    /// <exception cref="InvalidOperationException">指定服务类型未注册时抛出。</exception>
    object LazyGetRequiredService(Type serviceType);

    /// <summary>
    /// 延迟获取可选服务。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>服务实例；未注册时可能为 <c>null</c>。</returns>
    T LazyGetService<T>();

    /// <summary>
    /// 延迟获取指定类型的可选服务。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <returns>服务实例；未注册时可能为 <c>null</c>。</returns>
    object LazyGetService(Type serviceType);

    /// <summary>
    /// 延迟获取可选服务；未注册时返回默认值。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="defaultValue">服务未注册时返回的默认值。</param>
    /// <returns>服务实例或 <paramref name="defaultValue"/>。</returns>
    T LazyGetService<T>(T defaultValue);

    /// <summary>
    /// 延迟获取指定类型的可选服务；未注册时返回默认值。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <param name="defaultValue">服务未注册时返回的默认值。</param>
    /// <returns>服务实例或 <paramref name="defaultValue"/>。</returns>
    object LazyGetService(Type serviceType, object defaultValue);

    /// <summary>
    /// 延迟获取指定类型的服务；首次解析时由工厂创建。
    /// </summary>
    /// <param name="serviceType">要解析的服务类型。</param>
    /// <param name="factory">首次解析时用于创建服务实例的工厂。</param>
    /// <returns>服务实例。</returns>
    object LazyGetService(Type serviceType, Func<IServiceProvider, object> factory);

    /// <summary>
    /// 延迟获取指定服务；首次解析时由工厂创建。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="factory">首次解析时用于创建服务实例的工厂。</param>
    /// <returns>服务实例。</returns>
    T LazyGetService<T>(Func<IServiceProvider, object> factory);
}
