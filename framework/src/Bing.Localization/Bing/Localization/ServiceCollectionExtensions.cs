using Bing.Localization.Json;
using Bing.Localization.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Localization;

/// <summary>
/// 服务集合扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册Json本地化
    /// </summary>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services) =>
        AddJsonLocalization(services, "Resources");

    /// <summary>
    /// 注册Json本地化
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="resourcesPath">资源路径</param>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, string resourcesPath) =>
        services.AddJsonLocalization(t => t.ResourcesPath = resourcesPath);

    /// <summary>
    /// 注册Json本地化
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">Json本地化配置操作</param>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, Action<JsonLocalizationOptions> setupAction)
    {
        var options = new JsonLocalizationOptions();
        setupAction?.Invoke(options);
        if (setupAction != null)
            services.Configure(setupAction);
        services.AddMemoryCache();
        services.RemoveAll(typeof(IStringLocalizerFactory));
        services.RemoveAll(typeof(IStringLocalizer<>));
        services.RemoveAll(typeof(IStringLocalizer));
        services.TryAddSingleton<IPathResolver, PathResolver>();
        services.TryAddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.TryAddTransient(typeof(IStringLocalizer), typeof(StringLocalizer));
        return services;
    }

    /// <summary>
    /// 配置基于数据存储的本地化
    /// </summary>
    /// <typeparam name="TStore">本地化资源存储器类型</typeparam>
    /// <param name="services">服务集合</param>
    public static IServiceCollection AddStoreLocalization<TStore>(this IServiceCollection services)
        where TStore : ILocalizedStore => services.AddStoreLocalization<TStore>(options => options.Expiration = 28800);

    /// <summary>
    /// 配置基于数据存储的本地化
    /// </summary>
    /// <typeparam name="TStore">本地化资源存储器类型</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">本地化配置操作</param>
    public static IServiceCollection AddStoreLocalization<TStore>(this IServiceCollection services, Action<LocalizationOptions> setupAction)
        where TStore : ILocalizedStore
    {
        var options = new LocalizationOptions();
        setupAction?.Invoke(options);
        if (setupAction != null)
            services.Configure(setupAction);
        services.AddMemoryCache();
        services.RemoveAll(typeof(IStringLocalizerFactory));
        services.RemoveAll(typeof(IStringLocalizer<>));
        services.RemoveAll(typeof(IStringLocalizer));
        services.TryAddSingleton<IStringLocalizerFactory, StoreStringLocalizerFactory>();
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.TryAddTransient(typeof(IStringLocalizer), typeof(StringLocalizer));
        services.TryAddTransient(typeof(ILocalizedStore), typeof(TStore));
        services.TryAddTransient<ILocalizedManager, LocalizedManager>();
        return services;
    }
}
