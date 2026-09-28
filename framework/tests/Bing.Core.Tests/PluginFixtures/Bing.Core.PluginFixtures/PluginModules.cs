using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Core.PluginScanFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.PluginFixtures;

/// <summary>
/// 声明独立插件附加程序集的模块。
/// </summary>
[BingModuleAssembly(typeof(PluginScanMarker))]
public sealed class FixtureAdditionalAssemblyModule : BingModule { }

/// <summary>
/// 保存插件测试模块的生命周期与服务注册计数。
/// </summary>
public static class PluginFixtureState
{
    /// <summary>
    /// 记录当前测试轮次的插件服务注册次数。
    /// </summary>
    private static int _addServicesCount;
    /// <summary>
    /// 记录当前测试轮次的顶层模块服务注册次数。
    /// </summary>
    private static int _topModuleAddServicesCount;
    /// <summary>
    /// 记录当前测试轮次的顶层模块初始化次数。
    /// </summary>
    private static int _topModuleUseModuleCount;
    /// <summary>
    /// 记录当前测试轮次的异步初始化次数。
    /// </summary>
    private static int _asyncInitializeCount;
    /// <summary>
    /// 记录当前测试轮次的同步初始化次数。
    /// </summary>
    private static int _asyncUseModuleCount;
    /// <summary>
    /// 记录当前测试轮次的异步关闭次数。
    /// </summary>
    private static int _asyncShutdownCount;
    /// <summary>
    /// 记录当前测试轮次的异步资源释放次数。
    /// </summary>
    private static int _asyncDisposeCount;
    /// <summary>
    /// 通知当前测试轮次的异步初始化已开始。
    /// </summary>
    private static TaskCompletionSource<bool> _asyncInitializeEntered = CreateSignal();
    /// <summary>
    /// 控制当前测试轮次的异步初始化继续执行。
    /// </summary>
    private static TaskCompletionSource<bool> _asyncInitializeRelease = CreateSignal();

    /// <summary>
    /// 获取插件服务注册次数。
    /// </summary>
    public static int AddServicesCount => Volatile.Read(ref _addServicesCount);

    /// <summary>
    /// 获取顶层模块服务注册次数。
    /// </summary>
    public static int TopModuleAddServicesCount => Volatile.Read(ref _topModuleAddServicesCount);

    /// <summary>
    /// 获取顶层模块启用次数。
    /// </summary>
    public static int TopModuleUseModuleCount => Volatile.Read(ref _topModuleUseModuleCount);

    /// <summary>
    /// 获取插件异步初始化次数。
    /// </summary>
    public static int AsyncInitializeCount => Volatile.Read(ref _asyncInitializeCount);

    /// <summary>
    /// 获取插件同步初始化次数。
    /// </summary>
    public static int AsyncUseModuleCount => Volatile.Read(ref _asyncUseModuleCount);

    /// <summary>
    /// 获取插件异步关闭次数。
    /// </summary>
    public static int AsyncShutdownCount => Volatile.Read(ref _asyncShutdownCount);

    /// <summary>
    /// 获取插件异步资源释放次数。
    /// </summary>
    public static int AsyncDisposeCount => Volatile.Read(ref _asyncDisposeCount);

    /// <summary>
    /// 获取插件异步初始化已进入信号。
    /// </summary>
    public static Task AsyncInitializeEntered => _asyncInitializeEntered.Task;

    /// <summary>
    /// 增加插件服务注册计数。
    /// </summary>
    public static void RecordAddServices() => Interlocked.Increment(ref _addServicesCount);

    /// <summary>
    /// 增加顶层模块服务注册计数。
    /// </summary>
    public static void RecordTopModuleAddServices() => Interlocked.Increment(ref _topModuleAddServicesCount);

    /// <summary>
    /// 增加顶层模块启用计数。
    /// </summary>
    public static void RecordTopModuleUseModule() => Interlocked.Increment(ref _topModuleUseModuleCount);

    /// <summary>
    /// 增加插件异步初始化计数。
    /// </summary>
    public static void RecordAsyncInitialize()
    {
        Interlocked.Increment(ref _asyncInitializeCount);
        _asyncInitializeEntered.TrySetResult(true);
    }

    /// <summary>
    /// 增加插件同步初始化计数。
    /// </summary>
    public static void RecordAsyncUseModule() => Interlocked.Increment(ref _asyncUseModuleCount);

    /// <summary>
    /// 增加插件异步关闭计数。
    /// </summary>
    public static void RecordAsyncShutdown() => Interlocked.Increment(ref _asyncShutdownCount);

    /// <summary>
    /// 增加插件异步资源释放计数。
    /// </summary>
    public static void RecordAsyncDispose() => Interlocked.Increment(ref _asyncDisposeCount);

    /// <summary>
    /// 释放插件异步初始化等待。
    /// </summary>
    public static void ReleaseAsyncInitialization() => _asyncInitializeRelease.TrySetResult(true);

    /// <summary>
    /// 等待插件异步初始化继续执行。
    /// </summary>
    /// <param name="cancellationToken">用于取消等待的令牌。</param>
    public static Task WaitForAsyncInitializationRelease(CancellationToken cancellationToken) =>
        _asyncInitializeRelease.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// 清零插件测试状态。
    /// </summary>
    public static void Reset()
    {
        Interlocked.Exchange(ref _addServicesCount, 0);
        Interlocked.Exchange(ref _topModuleAddServicesCount, 0);
        Interlocked.Exchange(ref _topModuleUseModuleCount, 0);
        Interlocked.Exchange(ref _asyncInitializeCount, 0);
        Interlocked.Exchange(ref _asyncUseModuleCount, 0);
        Interlocked.Exchange(ref _asyncShutdownCount, 0);
        Interlocked.Exchange(ref _asyncDisposeCount, 0);
        _asyncInitializeEntered = CreateSignal();
        _asyncInitializeRelease = CreateSignal();
    }

    /// <summary>
    /// 创建支持异步延续的测试信号。
    /// </summary>
    /// <returns>未完成的测试信号。</returns>
    private static TaskCompletionSource<bool> CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// 插件测试服务接口。
/// </summary>
public interface IPluginFixtureService { }

/// <summary>
/// 用于验证插件程序集自动扫描的服务。
/// </summary>
[Dependency(ServiceLifetime.Singleton)]
public sealed class PluginFixtureService : IPluginFixtureService
{
    /// <summary>
    /// 初始化插件测试服务。
    /// </summary>
    public PluginFixtureService() => PluginFixtureState.RecordAddServices();
}

/// <summary>
/// 菱形依赖的叶子模块。
/// </summary>
public sealed class FixtureLeafModule : BingModule
{
    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        PluginFixtureState.RecordAddServices();
        return services;
    }
}

/// <summary>
/// 菱形依赖的左侧模块。
/// </summary>
[DependsOnModule(typeof(FixtureLeafModule))]
public sealed class FixtureLeftModule : BingModule { }

/// <summary>
/// 菱形依赖的右侧模块。
/// </summary>
[DependsOnModule(typeof(FixtureLeafModule))]
public sealed class FixtureRightModule : BingModule { }

/// <summary>
/// 插件测试使用的顶层模块。
/// </summary>
[DependsOnModule(typeof(FixtureLeftModule), typeof(FixtureRightModule))]
public sealed class FixtureTopModule : BingModule
{
    /// <inheritdoc />
    public override IServiceCollection AddServices(IServiceCollection services)
    {
        PluginFixtureState.RecordTopModuleAddServices();
        return services;
    }

    /// <inheritdoc />
    public override void UseModule(IServiceProvider provider)
    {
        PluginFixtureState.RecordTopModuleUseModule();
        base.UseModule(provider);
    }
}

/// <summary>
/// 用于验证多个插件入口可以共享同一程序集的独立模块。
/// </summary>
public sealed class FixtureIndependentModule : BingModule { }

/// <summary>
/// 用于验证插件异步初始化、关闭和资源释放的模块。
/// </summary>
public sealed class FixtureAsyncLifecycleModule : BingModule, IBingAsyncModuleInitializer,
    IBingAsyncModuleShutdown, IAsyncDisposable
{
    /// <inheritdoc />
    public async Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
    {
        PluginFixtureState.RecordAsyncInitialize();
        await PluginFixtureState.WaitForAsyncInitializationRelease(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void UseModule(IServiceProvider provider)
    {
        PluginFixtureState.RecordAsyncUseModule();
        base.UseModule(provider);
    }

    /// <inheritdoc />
    public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
    {
        PluginFixtureState.RecordAsyncShutdown();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        PluginFixtureState.RecordAsyncDispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// 向宿主记录动态插件的初始化、关闭和释放顺序。
/// </summary>
public sealed class FixtureHotLifecycleModule : BingModule, IBingAsyncModuleInitializer,
    IBingAsyncModuleShutdown, IAsyncDisposable
{
    /// <summary>
    /// 保存初始化期间取得的宿主生命周期记录回调。
    /// </summary>
    private Action<string> _record;

    /// <inheritdoc />
    public Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
    {
        _record = context.ServiceProvider.GetService<Action<string>>();
        _record?.Invoke("initialize");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
    {
        _record?.Invoke("shutdown");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _record?.Invoke("dispose");
        _record = null;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// 用于验证抽象启动模块被拒绝的类型。
/// </summary>
public abstract class FixtureAbstractModule : BingModule { }

/// <summary>
/// 用于验证无公共构造函数启动模块被拒绝的类型。
/// </summary>
public sealed class FixtureNoPublicConstructorModule : BingModule
{
    /// <summary>
    /// 初始化无公共构造函数的测试模块。
    /// </summary>
    private FixtureNoPublicConstructorModule() { }
}

/// <summary>
/// 用于验证非公开启动模块被拒绝的类型。
/// </summary>
internal sealed class FixtureInternalModule : BingModule { }

/// <summary>
/// 用于验证普通类型不能作为启动模块。
/// </summary>
public sealed class FixtureOrdinaryType { }

/// <summary>
/// 用于向插件前置配置传递宿主独享值的测试标记。
/// </summary>
public sealed class PluginConfigurationMarker
{
    /// <summary>
    /// 初始化配置标记。
    /// </summary>
    /// <param name="value">宿主配置值。</param>
    public PluginConfigurationMarker(string value) => Value = value;

    /// <summary>
    /// 获取宿主配置值。
    /// </summary>
    public string Value { get; }
}

/// <summary>
/// 暴露插件读取到的宿主配置值。
/// </summary>
public interface IPluginObservedConfiguration
{
    /// <summary>
    /// 获取插件读取到的配置值。
    /// </summary>
    string Value { get; }
}

/// <summary>
/// 插件配置值的只读实现。
/// </summary>
public sealed class PluginObservedConfiguration : IPluginObservedConfiguration
{
    /// <summary>
    /// 初始化插件配置值。
    /// </summary>
    /// <param name="value">插件读取到的配置值。</param>
    public PluginObservedConfiguration(string value) => Value = value;

    /// <inheritdoc />
    public string Value { get; }
}

/// <summary>
/// 在前置配置阶段读取当前服务集合配置的模块。
/// </summary>
public sealed class FixtureConfigurationModule : BingModule, IBingPreConfigureServices
{
    /// <inheritdoc />
    public new void PreConfigureServices(IServiceCollection services)
    {
        var marker = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(PluginConfigurationMarker))
            ?.ImplementationInstance as PluginConfigurationMarker;
        services.AddSingleton<IPluginObservedConfiguration>(new PluginObservedConfiguration(marker?.Value));
    }
}

/// <summary>
/// 用于验证插件入口模块内部依赖环的第一个模块。
/// </summary>
[DependsOnModule(typeof(FixtureCycleBModule))]
public sealed class FixtureCycleAModule : BingModule { }

/// <summary>
/// 用于验证插件入口模块内部依赖环的第二个模块。
/// </summary>
[DependsOnModule(typeof(FixtureCycleAModule))]
public sealed class FixtureCycleBModule : BingModule { }

/// <summary>
/// 用于验证热更新预初始化失败的模块。
/// </summary>
public sealed class FixtureHotPreFailureModule : BingModule
{
    /// <inheritdoc />
    public override void OnPreApplicationInitialization(BingModuleInitializationContext context)
    {
        context.ServiceProvider.GetService<Action<WeakReference>>()?.Invoke(
            new WeakReference(AssemblyLoadContext.GetLoadContext(GetType().Assembly)));
        throw new InvalidOperationException("candidate pre failure");
    }
}

/// <summary>
/// 用于验证热更新后初始化失败的模块。
/// </summary>
public sealed class FixtureHotPostFailureModule : BingModule
{
    /// <inheritdoc />
    public override void OnPostApplicationInitialization(BingModuleInitializationContext context)
    {
        context.ServiceProvider.GetService<Action<WeakReference>>()?.Invoke(
            new WeakReference(AssemblyLoadContext.GetLoadContext(GetType().Assembly)));
        throw new InvalidOperationException("candidate post failure");
    }
}

/// <summary>
/// 用于验证热更新配置失败或取消时异步释放候选模块的基类。
/// </summary>
public abstract class FixtureHotConfigurationAsyncDisposableModuleBase : BingModule, IAsyncDisposable
{
    /// <summary>
    /// 保存候选模块释放时调用的宿主计数回调。
    /// </summary>
    private Action<int> _recordDispose;

    /// <summary>
    /// 保存候选模块释放时等待的宿主异步门控。
    /// </summary>
    private Func<Task> _disposeGate;

    /// <summary>
    /// 捕获宿主提供的释放计数和可选门控回调。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    protected void CaptureAsyncDisposal(IServiceCollection services)
    {
        _recordDispose = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Action<int>))
            ?.ImplementationInstance as Action<int>;
        _disposeGate = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Func<Task>))
            ?.ImplementationInstance as Func<Task>;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        var recordDispose = _recordDispose;
        var disposeGate = _disposeGate;
        _disposeGate = null;
        recordDispose?.Invoke(1);
        return disposeGate == null ? ValueTask.CompletedTask : new ValueTask(disposeGate());
    }
}

/// <summary>
/// 用于验证热更新异步服务配置失败的模块基类。
/// </summary>
public abstract class FixtureHotConfigurationFailureModuleBase : FixtureHotConfigurationAsyncDisposableModuleBase
{
    /// <summary>
    /// 记录当前插件上下文并抛出阶段异常。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    /// <param name="message">异常消息。</param>
    protected Task Fail(IServiceCollection services, string message)
    {
        CaptureAsyncDisposal(services);
        CaptureContext(services);
        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 记录当前插件程序集的加载上下文。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    private void CaptureContext(IServiceCollection services) =>
        (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Action<WeakReference>))
            ?.ImplementationInstance as Action<WeakReference>)?.Invoke(
                new WeakReference(AssemblyLoadContext.GetLoadContext(GetType().Assembly)));
}

/// <summary>
/// 用于验证热更新前配置异步失败的模块。
/// </summary>
public sealed class FixtureHotPreConfigurationFailureModule : FixtureHotConfigurationFailureModuleBase
{
    /// <inheritdoc />
    public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Fail(services, "candidate pre configure failure");
}

/// <summary>
/// 用于验证热更新主配置异步失败的模块。
/// </summary>
public sealed class FixtureHotConfigurationFailureModule : FixtureHotConfigurationFailureModuleBase
{
    /// <inheritdoc />
    public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Fail(services, "candidate configure failure");
}

/// <summary>
/// 用于验证热更新后配置异步失败的模块。
/// </summary>
public sealed class FixtureHotPostConfigurationFailureModule : FixtureHotConfigurationFailureModuleBase
{
    /// <inheritdoc />
    public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Fail(services, "candidate post configure failure");
}

/// <summary>
/// 用于验证热更新异步服务配置合作式取消的模块基类。
/// </summary>
public abstract class FixtureHotConfigurationCancellationModuleBase : FixtureHotConfigurationAsyncDisposableModuleBase
{
    /// <summary>
    /// 记录候选上下文并请求取消。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    /// <param name="phase">当前服务配置阶段。</param>
    protected Task Cancel(IServiceCollection services, string phase)
    {
        CaptureAsyncDisposal(services);
        CaptureContext(services);
        (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Action<string>))
            ?.ImplementationInstance as Action<string>)?.Invoke(phase);
        (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Action))
            ?.ImplementationInstance as Action)?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 记录当前插件程序集的加载上下文。
    /// </summary>
    /// <param name="services">当前服务集合。</param>
    private void CaptureContext(IServiceCollection services) =>
        (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(Action<WeakReference>))
            ?.ImplementationInstance as Action<WeakReference>)?.Invoke(
                new WeakReference(AssemblyLoadContext.GetLoadContext(GetType().Assembly)));
}

/// <summary>
/// 用于验证热更新前配置异步合作式取消的模块。
/// </summary>
public sealed class FixtureHotPreConfigurationCancellationModule : FixtureHotConfigurationCancellationModuleBase
{
    /// <inheritdoc />
    public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Cancel(services, "PreConfigureServices");
}

/// <summary>
/// 用于验证热更新主配置异步合作式取消的模块。
/// </summary>
public sealed class FixtureHotConfigurationCancellationModule : FixtureHotConfigurationCancellationModuleBase
{
    /// <inheritdoc />
    public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Cancel(services, "AddServices");
}

/// <summary>
/// 用于验证热更新后配置异步合作式取消的模块。
/// </summary>
public sealed class FixtureHotPostConfigurationCancellationModule : FixtureHotConfigurationCancellationModuleBase
{
    /// <inheritdoc />
    public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        Cancel(services, "PostConfigureServices");
}

/// <summary>
/// 用于验证后置初始化尚未完成时不发布候选代的模块。
/// </summary>
public sealed class FixtureHotPendingPostModule : BingModule
{
    /// <inheritdoc />
    public override async Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
        CancellationToken cancellationToken)
    {
        context.ServiceProvider.GetRequiredService<Action>().Invoke();
        await context.ServiceProvider.GetRequiredService<TaskCompletionSource<bool>>().Task
            .WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
