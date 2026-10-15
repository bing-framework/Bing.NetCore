using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供模块关闭期间使用的服务。
/// </summary>
/// <remarks>
/// 服务作用域只在本轮关闭期间有效，模块不得长期保存其中的作用域服务。
/// </remarks>
public sealed class BingModuleShutdownContext
{
    /// <summary>
    /// 初始化一个 <see cref="BingModuleShutdownContext" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">本轮关闭使用的服务提供程序。</param>
    /// <exception cref="ArgumentNullException">服务提供程序为空时抛出。</exception>
    public BingModuleShutdownContext(IServiceProvider serviceProvider) =>
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// 获取用于短期清理的服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
