using Bing.Localization.Json;
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
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, string resourcesPath)
    {
        services.AddMemoryCache();
        services.RemoveAll(typeof(IStringLocalizerFactory));
        services.RemoveAll(typeof(IStringLocalizer<>));
        services.RemoveAll(typeof(IStringLocalizer));
        services.TryAddSingleton<IPathResolver, PathResolver>();
        services.TryAddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
        services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
        services.TryAddTransient<IStringLocalizer, StringLocalizer>();
        services.Configure<Microsoft.Extensions.Localization.LocalizationOptions>(options => options.ResourcesPath = resourcesPath);
        return services;
    }

    /// <summary>
    /// 注册Json本地化
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">Json本地化配置操作</param>
    public static IServiceCollection AddJsonLocalization(this IServiceCollection services, Action<JsonLocalizationOptions> setupAction)
    {
        var options = new JsonLocalizationOptions();
        setupAction?.Invoke(options);
        services.AddJsonLocalization(options.ResourcesPath);
        return services;
    }
}
