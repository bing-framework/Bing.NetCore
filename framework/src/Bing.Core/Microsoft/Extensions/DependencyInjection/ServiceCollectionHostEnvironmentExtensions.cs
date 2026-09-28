using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务集合的通用宿主环境读取扩展。
/// </summary>
public static class ServiceCollectionHostEnvironmentExtensions
{
    /// <summary>
    /// 获取已注册的宿主环境实例；未直接注册时从宿主构建上下文读取。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>直接注册或宿主构建上下文中的宿主环境实例。</returns>
    /// <exception cref="Bing.BingFrameworkException">未注册实例或注册期无法读取工厂/类型注册时抛出。</exception>
    public static IHostEnvironment GetHostEnvironment(this IServiceCollection services) =>
        ServiceCollectionInstanceReader.GetRequired<IHostEnvironment>(services, GetHostEnvironmentOrNull);

    /// <summary>
    /// 获取已注册的宿主环境实例；未直接注册时尝试从宿主构建上下文读取。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>直接注册或宿主构建上下文中的宿主环境实例；不可在注册期读取时返回 null。</returns>
    public static IHostEnvironment GetHostEnvironmentOrNull(this IServiceCollection services)
    {
        if (ServiceCollectionInstanceReader.TryGet<IHostEnvironment>(services, out var environment, out var invalidRegistration))
            return environment;
        if (invalidRegistration)
            return null;

        if (ServiceCollectionInstanceReader.TryGet<HostBuilderContext>(services, out var hostBuilderContext, out _))
            return hostBuilderContext.HostingEnvironment;

        return null;
    }
}
