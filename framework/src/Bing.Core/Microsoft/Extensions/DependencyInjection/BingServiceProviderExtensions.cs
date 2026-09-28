using System.Diagnostics;
using Bing.Core.Modularity;
using Bing.Helpers;
using Bing.Reflection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务提供程序(<see cref="IServiceProvider"/>) 扩展
/// </summary>
public static partial class BingServiceProviderExtensions
{
    /// <summary>
    /// 框架初始化
    /// </summary>
    private const string FrameworkLog = "BingFrameworkLog";

    /// <summary>
    /// 根据指定名称开头获取服务
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="name">名称</param>
    /// <returns>名称以指定字符串开头的服务实例；未找到时返回 null。</returns>
    public static T GetStartWith<T>(this IServiceProvider serviceProvider, string name) where T : class
    {
        var services = serviceProvider.GetServices<T>();
        return services.FirstOrDefault(m => m.GetType().Name.ToString().StartsWith(name));
    }

    /// <summary>
    /// 根据指定名称结尾获取服务
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="name">名称</param>
    /// <returns>名称以指定字符串结尾的服务实例；未找到时返回 null。</returns>
    public static T GetEndsWith<T>(this IServiceProvider serviceProvider, string name) where T : class
    {
        var services = serviceProvider.GetServices<T>();
        return services.FirstOrDefault(m => m.GetType().Name.ToString().EndsWith(name));
    }

    /// <summary>
    /// 获取所有模块信息
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>按模块级别、顺序和类型名称排序后的模块数组。</returns>
    public static BingModule[] GetAllModules(this IServiceProvider serviceProvider) =>
        serviceProvider.GetServices<BingModule>()
            .OrderBy(m => m.Level)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.GetType().FullName)
            .ToArray();

    /// <summary>
    /// Bing模块初始化，适用于非AspNetCore环境
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>完成 Bing 模块初始化后的服务提供程序。</returns>
    public static IServiceProvider UseBing(this IServiceProvider serviceProvider)
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
        serviceProvider.GetRequiredService<IBingModuleManager>().Initialize();
        return serviceProvider;
    }

    /// <summary>
    /// 异步初始化当前应用的模块。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="cancellationToken">初始化取消令牌。</param>
    /// <returns>完成 Bing 模块初始化后的服务提供程序。</returns>
    public static async Task<IServiceProvider> UseBingAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
        await serviceProvider.GetRequiredService<IBingModuleManager>()
            .InitializeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return serviceProvider;
    }

    /// <summary>
    /// 同步停止模块并释放模块自持有资源。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <remarks>该方法不释放根容器。</remarks>
    public static void ShutdownBing(this IServiceProvider serviceProvider) =>
        (serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider))).GetRequiredService<IBingModuleManager>().Shutdown();

    /// <summary>
    /// 异步停止模块并释放模块自持有资源。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="cancellationToken">关闭取消令牌。</param>
    /// <returns>表示关闭操作的任务。</returns>
    /// <remarks>调用方应在释放根容器之前等待此任务完成。</remarks>
    public static Task ShutdownBingAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default) =>
        (serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider))).GetRequiredService<IBingModuleManager>().ShutdownAsync(cancellationToken);

    /// <summary>
    /// 获取指定类型的日志对象
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>日志对象</returns>
    public static ILogger<T> GetLogger<T>(this IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger<T>();
    }

    /// <summary>
    /// 获取指定类型的日志对象
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="type">类型</param>
    /// <returns>日志对象</returns>
    public static ILogger GetLogger(this IServiceProvider serviceProvider, Type type)
    {
        Check.NotNull(type, nameof(type));
        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger(type);
    }

    /// <summary>
    /// 获取指定对象给类型的日志对象
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="instance">要获取日志的类型对象，一般指当前类，即this</param>
    /// <returns>日志对象</returns>
    public static ILogger GetLogger(this IServiceProvider serviceProvider, object instance)
    {
        Check.NotNull(instance, nameof(instance));
        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger(instance.GetType());
    }

    /// <summary>
    /// 获取指定名称的日志对象
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="name">名称</param>
    /// <returns>日志对象</returns>
    public static ILogger GetLogger(this IServiceProvider serviceProvider, string name)
    {
        var factory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger(name);
    }
}
