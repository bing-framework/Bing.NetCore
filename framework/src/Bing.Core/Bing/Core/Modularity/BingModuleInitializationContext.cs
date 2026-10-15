using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供模块初始化期间使用的服务和宿主上下文。
/// </summary>
/// <remarks>
/// 新入口提供的服务作用域仅在本轮初始化期间有效，模块不得长期保存其中的作用域服务。
/// </remarks>
public sealed class BingModuleInitializationContext
{
    /// <summary>
    /// 初始化一个 <see cref="BingModuleInitializationContext" /> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">本轮初始化使用的服务提供程序。</param>
    /// <param name="hostContext">可选的宿主上下文对象。</param>
    /// <param name="initializeModule">可选的宿主适配模块分派器。</param>
    /// <exception cref="ArgumentNullException">服务提供程序为空时抛出。</exception>
    public BingModuleInitializationContext(IServiceProvider serviceProvider, object hostContext = null,
        Action<BingModule, IServiceProvider> initializeModule = null)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        HostContext = hostContext;
        InitializeModule = initializeModule;
    }

    /// <summary>
    /// 获取本次初始化使用的服务提供程序。
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取可选的宿主上下文对象。
    /// </summary>
    public object HostContext { get; }

    /// <summary>
    /// 获取宿主适配使用的模块分派器。
    /// </summary>
    internal Action<BingModule, IServiceProvider> InitializeModule { get; }
}
