using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供模块生命周期上下文的配置和宿主环境便捷访问。
/// </summary>
public static class BingModuleContextExtensions
{
    /// <summary>
    /// 获取初始化期间当前服务作用域中的配置。
    /// </summary>
    /// <param name="context">初始化上下文。</param>
    /// <returns>当前容器解析的配置实例。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    /// <exception cref="Bing.BingFrameworkException">当前服务提供程序未注册配置时抛出。</exception>
    public static IConfiguration GetConfiguration(this BingModuleInitializationContext context) =>
        GetRequired<IConfiguration>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 尝试获取初始化期间当前服务作用域中的配置。
    /// </summary>
    /// <param name="context">初始化上下文。</param>
    /// <returns>当前容器解析的配置实例；未注册时返回 null。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    public static IConfiguration GetConfigurationOrNull(this BingModuleInitializationContext context) =>
        GetOptional<IConfiguration>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 获取初始化期间当前服务作用域中的宿主环境。
    /// </summary>
    /// <param name="context">初始化上下文。</param>
    /// <returns>当前容器解析的宿主环境。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    /// <exception cref="Bing.BingFrameworkException">当前服务提供程序未注册宿主环境时抛出。</exception>
    public static IHostEnvironment GetHostEnvironment(this BingModuleInitializationContext context) =>
        GetRequired<IHostEnvironment>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 尝试获取初始化期间当前服务作用域中的宿主环境。
    /// </summary>
    /// <param name="context">初始化上下文。</param>
    /// <returns>当前容器解析的宿主环境；未注册时返回 null。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    public static IHostEnvironment GetHostEnvironmentOrNull(this BingModuleInitializationContext context) =>
        GetOptional<IHostEnvironment>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 获取关闭期间当前服务作用域中的配置。
    /// </summary>
    /// <param name="context">关闭上下文。</param>
    /// <returns>当前容器解析的配置实例。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    /// <exception cref="Bing.BingFrameworkException">当前服务提供程序未注册配置时抛出。</exception>
    public static IConfiguration GetConfiguration(this BingModuleShutdownContext context) =>
        GetRequired<IConfiguration>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 尝试获取关闭期间当前服务作用域中的配置。
    /// </summary>
    /// <param name="context">关闭上下文。</param>
    /// <returns>当前容器解析的配置实例；未注册时返回 null。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    public static IConfiguration GetConfigurationOrNull(this BingModuleShutdownContext context) =>
        GetOptional<IConfiguration>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 获取关闭期间当前服务作用域中的宿主环境。
    /// </summary>
    /// <param name="context">关闭上下文。</param>
    /// <returns>当前容器解析的宿主环境。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    /// <exception cref="Bing.BingFrameworkException">当前服务提供程序未注册宿主环境时抛出。</exception>
    public static IHostEnvironment GetHostEnvironment(this BingModuleShutdownContext context) =>
        GetRequired<IHostEnvironment>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 尝试获取关闭期间当前服务作用域中的宿主环境。
    /// </summary>
    /// <param name="context">关闭上下文。</param>
    /// <returns>当前容器解析的宿主环境；未注册时返回 null。</returns>
    /// <exception cref="ArgumentNullException">上下文为空时抛出。</exception>
    public static IHostEnvironment GetHostEnvironmentOrNull(this BingModuleShutdownContext context) =>
        GetOptional<IHostEnvironment>(context?.ServiceProvider, nameof(context));

    /// <summary>
    /// 从当前作用域获取必需服务。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="serviceProvider">当前生命周期的服务提供程序。</param>
    /// <param name="contextName">空上下文对应的参数名。</param>
    /// <returns>已注册的服务实例。</returns>
    private static T GetRequired<T>(IServiceProvider serviceProvider, string contextName) where T : class
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(contextName);
        return serviceProvider.GetService<T>() ?? throw new Bing.BingFrameworkException(
            "Could not resolve " + typeof(T).AssemblyQualifiedName + " from the module context service provider.");
    }

    /// <summary>
    /// 从当前作用域尝试获取服务。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <param name="serviceProvider">当前生命周期的服务提供程序。</param>
    /// <param name="contextName">空上下文对应的参数名。</param>
    /// <returns>已注册的服务实例；未注册时返回 null。</returns>
    private static T GetOptional<T>(IServiceProvider serviceProvider, string contextName) where T : class
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(contextName);
        return serviceProvider.GetService<T>();
    }
}
