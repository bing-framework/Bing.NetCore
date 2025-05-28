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
    public static IServiceCollection ReplaceConfiguration(this IServiceCollection services, IConfiguration configuration) =>
        services.Replace(ServiceDescriptor.Singleton<IConfiguration>(configuration));

    /// <summary>
    /// 获取 <see cref="IConfiguration"/> 配置对象。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>返回找到的 <see cref="IConfiguration"/> 对象。</returns>
    /// <exception cref="BingFrameworkException"></exception>
    public static IConfiguration GetConfiguration(this IServiceCollection services) =>
        services.GetConfigurationOrNull() ?? throw new BingFrameworkException("Could not find an implementation of " + typeof(IConfiguration).AssemblyQualifiedName + " in the service collection.");

    /// <summary>
    /// 获取 <see cref="IConfiguration"/> 配置对象。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>返回找到的 <see cref="IConfiguration"/> 对象；如果没有找到，则返回 null。</returns>
    public static IConfiguration GetConfigurationOrNull(this IServiceCollection services)
    {
        var hostBuilderContext = services.GetSingletonInstanceOrNull<HostBuilderContext>();
        if (hostBuilderContext?.Configuration != null)
            return hostBuilderContext.Configuration as IConfigurationRoot;
        return services.GetSingletonInstanceOrNull<IConfiguration>();
    }
}
