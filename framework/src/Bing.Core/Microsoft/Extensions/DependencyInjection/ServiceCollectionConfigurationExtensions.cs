using Bing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务集合 - 配置 扩展
/// </summary>
public static class ServiceCollectionConfigurationExtensions
{
    /// <summary>
    /// 在服务集合中替换现有的 <see cref="IConfiguration"/> 实例。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置对象</param>
    /// <returns>完成配置替换后的服务集合。</returns>
    public static IServiceCollection ReplaceConfiguration(this IServiceCollection services, IConfiguration configuration) =>
        services.Replace(ServiceDescriptor.Singleton<IConfiguration>(configuration));

    /// <summary>
    /// 获取 <see cref="IConfiguration"/> 配置对象。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>返回找到的 <see cref="IConfiguration"/> 对象。</returns>
    /// <exception cref="BingFrameworkException">服务集合中未找到配置对象时抛出。</exception>
    public static IConfiguration GetConfiguration(this IServiceCollection services) =>
        ServiceCollectionInstanceReader.GetRequired<IConfiguration>(services, GetConfigurationOrNull);

    /// <summary>
    /// 获取 <see cref="IConfiguration"/> 配置对象。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>返回找到的 <see cref="IConfiguration"/> 对象；如果没有找到，则返回 null。</returns>
    public static IConfiguration GetConfigurationOrNull(this IServiceCollection services)
    {
        if (ServiceCollectionInstanceReader.TryGet<IConfiguration>(services, out var configuration, out var invalidRegistration))
            return configuration;
        if (invalidRegistration)
            return null;

        if (ServiceCollectionInstanceReader.TryGet<HostBuilderContext>(services, out var hostBuilderContext, out _))
            return hostBuilderContext.Configuration;

        return null;
    }
}

/// <summary>
/// 从服务集合中读取已明确注册的单例实例，避免注册期构建容器或执行工厂。
/// </summary>
internal static class ServiceCollectionInstanceReader
{
    /// <summary>
    /// 尝试读取最后一个单例实例注册。
    /// </summary>
    /// <typeparam name="T">要读取的服务类型。</typeparam>
    /// <param name="services">当前服务集合。</param>
    /// <param name="value">读取到的实例；不存在时为 null。</param>
    /// <param name="invalidRegistration">是否存在不能在注册期读取的注册。</param>
    /// <returns>读取到单例实例时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    internal static bool TryGet<T>(IServiceCollection services, out T value, out bool invalidRegistration)
        where T : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var descriptor = services.LastOrDefault(item => item.ServiceType == typeof(T));
        invalidRegistration = descriptor != null &&
            (descriptor.Lifetime != ServiceLifetime.Singleton || descriptor.ImplementationInstance is not T);
        value = invalidRegistration ? null : descriptor?.ImplementationInstance as T;
        return value != null;
    }

    /// <summary>
    /// 读取实例；必需入口遇到工厂或类型注册时给出注册期迁移提示。
    /// </summary>
    /// <typeparam name="T">要读取的服务类型。</typeparam>
    /// <param name="services">当前服务集合。</param>
    /// <param name="resolver">未注册时使用的实例解析器。</param>
    /// <returns>已注册或由解析器提供的服务实例。</returns>
    internal static T GetRequired<T>(IServiceCollection services, Func<IServiceCollection, T> resolver)
        where T : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (TryGet<T>(services, out var value, out var invalidRegistration))
            return value;

        if (invalidRegistration)
            throw new BingFrameworkException($"注册期无法读取 {typeof(T).AssemblyQualifiedName}：请提供单例实例，或在运行期通过 DI 获取。");

        return resolver(services) ?? throw new BingFrameworkException(
            "Could not find an implementation of " + typeof(T).AssemblyQualifiedName + " in the service collection.");
    }
}
