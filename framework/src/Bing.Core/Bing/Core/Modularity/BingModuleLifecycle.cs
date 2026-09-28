using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 定义模块注册服务之前执行的可选配置阶段。
/// </summary>
public interface IBingPreConfigureServices
{
    /// <summary>
    /// 在当前应用的服务集合中执行前置配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    void PreConfigureServices(IServiceCollection services);
}

/// <summary>
/// 定义模块注册服务之后执行的可选配置阶段。
/// </summary>
public interface IBingPostConfigureServices
{
    /// <summary>
    /// 在当前应用的服务集合中执行后置配置。
    /// </summary>
    /// <param name="services">当前应用的服务集合。</param>
    void PostConfigureServices(IServiceCollection services);
}

/// <summary>
/// 提供模块初始化期间使用的服务和宿主上下文。
/// </summary>
/// <remarks>
/// 新入口提供的服务作用域仅在本轮初始化期间有效，模块不得长期保存其中的作用域服务。
/// </remarks>
public sealed class BingModuleInitializationContext
{
    /// <summary>
    /// 初始化模块初始化上下文。
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

/// <summary>
/// 提供模块关闭期间使用的服务。
/// </summary>
/// <remarks>
/// 服务作用域只在本轮关闭期间有效，模块不得长期保存其中的作用域服务。
/// </remarks>
public sealed class BingModuleShutdownContext
{
    /// <summary>
    /// 初始化模块关闭上下文。
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

/// <summary>
/// 定义模块异步初始化能力。
/// </summary>
/// <remarks>
/// 实现此接口的模块必须通过异步入口启动，框架不会再调用其同步初始化方法。
/// </remarks>
public interface IBingAsyncModuleInitializer
{
    /// <summary>
    /// 异步初始化模块。
    /// </summary>
    /// <param name="context">模块初始化上下文。</param>
    /// <param name="cancellationToken">用于取消初始化的令牌。</param>
    Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 定义模块同步停止能力。
/// </summary>
public interface IBingModuleShutdown
{
    /// <summary>
    /// 停止模块自持有的工作和订阅。
    /// </summary>
    /// <param name="context">模块关闭上下文。</param>
    void Shutdown(BingModuleShutdownContext context);
}

/// <summary>
/// 定义模块异步停止能力。
/// </summary>
public interface IBingAsyncModuleShutdown
{
    /// <summary>
    /// 异步停止模块自持有的工作和订阅。
    /// </summary>
    /// <param name="context">模块关闭上下文。</param>
    /// <param name="cancellationToken">用于取消关闭的令牌。</param>
    Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken);
}

/// <summary>
/// 管理应用所属模块的初始化和关闭生命周期。
/// </summary>
public interface IBingModuleManager
{
    /// <summary>
    /// 初始化模块一次。
    /// </summary>
    /// <param name="context">可选的模块初始化上下文。</param>
    /// <remarks>初始化失败后不可重试；包含异步模块时必须使用异步入口。</remarks>
    void Initialize(BingModuleInitializationContext context = null);

    /// <summary>
    /// 初始化模块一次。
    /// </summary>
    /// <param name="context">可选的模块初始化上下文。</param>
    /// <param name="cancellationToken">用于取消初始化的令牌。</param>
    /// <remarks>非 Web 并发请求共享同一次执行，重入请求会被拒绝。</remarks>
    Task InitializeAsync(BingModuleInitializationContext context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按逆序停止并释放模块。
    /// </summary>
    /// <remarks>包含仅异步关闭模块时必须使用异步入口。</remarks>
    void Shutdown();

    /// <summary>
    /// 按逆序停止并释放模块。
    /// </summary>
    /// <param name="cancellationToken">用于取消关闭的令牌。</param>
    /// <remarks>此方法不释放宿主根容器。</remarks>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
