using Bing.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.DynamicApi.Client;

/// <summary>
/// 注册动态 API 类型化客户端。
/// </summary>
public static class BingDynamicApiClientServiceCollectionExtensions
{
    /// <summary>
    /// 注册动态 API 客户端基础设施并配置远程服务。
    /// </summary>
    /// <param name="services">依赖注入服务集合。</param>
    /// <param name="configure">客户端选项配置。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingDynamicApiClient(this IServiceCollection services, Action<BingDynamicApiClientOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        services.AddOptions<BingDynamicApiClientOptions>().Configure(configure);
        services.AddHttpClient();
        services.TryAddSingleton<IBingDynamicApiRequestContext, EmptyBingDynamicApiRequestContext>();
        services.TryAddSingleton<IBingDynamicApiParameterConverter, DefaultBingDynamicApiParameterConverter>();
        services.TryAddSingleton<IBingDynamicApiJsonSerializer, SystemTextJsonBingDynamicApiSerializer>();
        services.TryAddSingleton<IBingDynamicApiRetryPolicy, DefaultBingDynamicApiRetryPolicy>();
        services.TryAddSingleton<BingDynamicApiDefinitionCache>();
        services.TryAddScoped<BingDynamicApiDefinitionProvider>();
        services.TryAddScoped<BingDynamicApiClientProxyFactory>();
        return services;
    }

    /// <summary>
    /// 为应用服务接口注册运行时 HTTP 代理。
    /// </summary>
    /// <typeparam name="TService">继承 <see cref="IAppService"/> 的共享服务接口。</typeparam>
    /// <param name="services">依赖注入服务集合。</param>
    /// <param name="remoteName">远程服务名称，默认是 <c>default</c>。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddBingDynamicApiClient<TService>(this IServiceCollection services, string remoteName = "default")
        where TService : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        var serviceType = typeof(TService);
        if (!serviceType.IsInterface || !typeof(IAppService).IsAssignableFrom(serviceType))
            throw new ArgumentException($"动态 API 客户端类型 '{serviceType.FullName}' 必须是继承 {nameof(IAppService)} 的接口。", nameof(TService));
        if (string.IsNullOrWhiteSpace(remoteName))
            throw new ArgumentException("远程服务名称不能为空。", nameof(remoteName));

        services.TryAddScoped<BingDynamicApiClientProxyFactory>();
        services.AddTransient(serviceType, provider => provider.GetRequiredService<BingDynamicApiClientProxyFactory>().Create(serviceType, remoteName));
        return services;
    }
}
