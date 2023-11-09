using Bing.DependencyInjection;
using Bing.Logging;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Internal;

/// <summary>
/// 内部服务集合扩展
/// </summary>
internal static class InternalServiceCollectionExtensions
{
    /// <summary>
    /// 注册核心服务（Options、Logging、Localization）
    /// </summary>
    /// <param name="services">服务集合</param>
    internal static void AddCoreServices(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddLogging();
        services.AddLocalization();
    }

    /// <summary>
    /// 注册核心 Bing 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    internal static void AddCoreBingServices(this IServiceCollection services)
    {
        var assemblyFinder = new AppDomainAllAssemblyFinder();
        var dependencyTypeFinder = new DependencyTypeFinder(assemblyFinder);

        services.TryAddSingleton<IAllAssemblyFinder>(assemblyFinder);
        services.TryAddSingleton<IDependencyTypeFinder>(dependencyTypeFinder);
        services.TryAddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());
    }
}
