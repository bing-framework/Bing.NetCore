using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证模块资源在正常、失败和取消路径上的释放次数。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleResourceRegressionTest
{
    /// <summary>
    /// 验证相等但不同实例的同步资源各释放一次。
    /// </summary>
    [Fact]
    public void EqualButDistinctModules_NormalShutdownAndRepeatedShutdown_DisposeEachInstanceOnce()
    {
        EqualResourceProbe.Reset();
        using var provider = Create<EqualRootModule>();

        provider.UseBing();
        provider.ShutdownBing();
        provider.ShutdownBing();

        EqualResourceProbe.ChildSyncDisposals.ShouldBe(1);
        EqualResourceProbe.RootSyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证未初始化直接关闭也会释放模块资源。
    /// </summary>
    [Fact]
    public void ExplicitShutdownBeforeInitialization_ReleasesOwnedModulesOnce()
    {
        EqualResourceProbe.Reset();
        using var provider = Create<EqualRootModule>();

        provider.ShutdownBing();
        provider.ShutdownBing();

        EqualResourceProbe.ChildSyncDisposals.ShouldBe(1);
        EqualResourceProbe.RootSyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证异步关闭和重复关闭只释放每个异步资源一次。
    /// </summary>
    [Fact]
    public async Task EqualButDistinctAsyncModules_AsyncShutdownAndRepeatedShutdown_DisposeEachInstanceOnce()
    {
        EqualResourceProbe.Reset();
        await using var provider = Create<EqualAsyncRootModule>();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        await provider.ShutdownBingAsync();

        EqualResourceProbe.ChildAsyncDisposals.ShouldBe(1);
        EqualResourceProbe.RootAsyncDisposals.ShouldBe(1);
        EqualResourceProbe.ChildSyncDisposals.ShouldBe(0);
        EqualResourceProbe.RootSyncDisposals.ShouldBe(0);
    }

    /// <summary>
    /// 验证配置失败时相等模块实例各释放一次。
    /// </summary>
    [Fact]
    public void EqualButDistinctModules_ConfigurationFailure_DisposeEachConstructedInstanceOnce()
    {
        EqualResourceProbe.Reset();

        Should.Throw<InvalidOperationException>(() => CreateServices<EqualFailingRootModule>());

        EqualResourceProbe.ChildSyncDisposals.ShouldBe(1);
        EqualResourceProbe.RootSyncDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证模块构造失败时释放此前已构造的实例。
    /// </summary>
    [Fact]
    public void ConstructorFailure_ReleasesPreviouslyConstructedModules()
    {
        ConstructionProbe.Reset();

        var error = Should.Throw<TargetInvocationException>(() => CreateServices<ThrowingConstructorRootModule>());

        error.InnerException.ShouldBeOfType<InvalidOperationException>();
        ConstructionProbe.RootDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证预取消关闭仍尝试全部钩子并释放所有资源。
    /// </summary>
    [Fact]
    public async Task ShutdownAsync_PreCanceledToken_StillAttemptsAllHooks_ReleasesAllAndAggregatesErrors()
    {
        var probe = new ShutdownCancellationProbe();
        CancellationShutdownModule.Probe = probe;
        await using var provider = Create<CancellationRootModule>();
        await provider.UseBingAsync();

        var error = await Assert.ThrowsAsync<AggregateException>(() =>
            provider.ShutdownBingAsync(new CancellationToken(canceled: true)));

        probe.HookAttempts.ShouldBe(new[] { nameof(CancellationRootModule), nameof(CancellationChildModule) });
        probe.SyncDisposals.ShouldBe(new[] { nameof(CancellationRootModule), nameof(CancellationChildModule) });
        error.InnerExceptions.Count.ShouldBe(2);
    }

    /// <summary>
    /// 验证关闭中途取消后继续处理剩余钩子并释放资源。
    /// </summary>
    [Fact]
    public async Task ShutdownAsync_MidFlightCancellation_ContinuesWithRemainingHooks_ReleasesAllAndAggregatesErrors()
    {
        var probe = new ShutdownCancellationProbe();
        CancellationShutdownModule.Probe = probe;
        await using var provider = Create<CancellationRootModule>();
        await provider.UseBingAsync();

        using var cancellation = new CancellationTokenSource();
        var shutdown = provider.ShutdownBingAsync(cancellation.Token);
        await probe.FirstHookEntered.Task;
        cancellation.Cancel();
        probe.ReleaseFirstHook.TrySetResult(true);

        var error = await Assert.ThrowsAsync<AggregateException>(() => shutdown);

        probe.HookAttempts.ShouldBe(new[] { nameof(CancellationRootModule), nameof(CancellationChildModule) });
        probe.SyncDisposals.ShouldBe(new[] { nameof(CancellationRootModule), nameof(CancellationChildModule) });
        error.InnerExceptions.Count.ShouldBe(2);
    }

    /// <summary>
    /// 验证纯异步释放模块的关闭和重复释放只执行一次。
    /// </summary>
    [Fact]
    public async Task AsyncDisposableOnlyModule_AsyncShutdownAndRepeatedDispose_ReleaseOnce()
    {
        AsyncOnlyProbe.Reset();
        var provider = Create<AsyncOnlyModule>();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        await provider.ShutdownBingAsync();
        await provider.DisposeAsync();

        AsyncOnlyProbe.Shutdowns.ShouldBe(1);
        AsyncOnlyProbe.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证配置失败时释放纯异步资源并保留原始异常。
    /// </summary>
    [Fact]
    public void AsyncDisposableOnlyModule_ConfigurationFailure_ReleasesAsyncResourceAndPreservesError()
    {
        AsyncOnlyProbe.Reset();

        var error = Should.Throw<InvalidOperationException>(() => CreateServices<AsyncOnlyConfigurationFailureRootModule>());

        error.Message.ShouldBe("intentional async-only configuration failure");
        AsyncOnlyProbe.ConfigurationRootDisposals.ShouldBe(1);
        AsyncOnlyProbe.ConfigurationDependencyDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证构造失败时释放纯异步资源并保留原始异常。
    /// </summary>
    [Fact]
    public void AsyncDisposableOnlyModule_ConstructorFailure_ReleasesAsyncResourceAndPreservesError()
    {
        AsyncOnlyProbe.Reset();

        var error = Should.Throw<TargetInvocationException>(() => CreateServices<AsyncOnlyConstructorRootModule>());

        error.InnerException.ShouldBeOfType<InvalidOperationException>();
        error.InnerException.Message.ShouldBe("intentional constructor failure");
        AsyncOnlyProbe.ConstructorDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证异步配置失败不会被捕获的同步上下文阻塞。
    /// </summary>
    [Fact]
    public async Task AsyncOnlyConfigurationFailure_DoesNotDeadlockCapturedSynchronizationContext()
    {
        AsyncOnlyProbe.Reset();

        var operation = Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());
            try
            {
                var error = Should.Throw<InvalidOperationException>(() =>
                    CreateServices<ContextCapturingAsyncOnlyConfigurationModule>());
                error.Message.ShouldBe("intentional context capturing configuration failure");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        });

        await operation.WaitAsync(TimeSpan.FromSeconds(2));
        AsyncOnlyProbe.ContextDisposals.ShouldBe(1);
    }

    /// <summary>
    /// 验证外部保留服务集合时，正常关闭仍能释放 provider 和模块对象图。
    /// </summary>
    [Fact]
    public void RetainedServiceCollection_NormalShutdown_AllowsProviderAndModuleCollection()
    {
        var snapshot = CreateRetainedNormalApplication();

        snapshot.Services.Count.ShouldBeGreaterThan(0);
        ForceCollection();

        snapshot.Provider.TryGetTarget(out _).ShouldBeFalse();
        snapshot.Module.TryGetTarget(out _).ShouldBeFalse();
        GC.KeepAlive(snapshot.Services);
    }

    /// <summary>
    /// 验证配置失败后外部保留服务集合不会继续保留已构造模块。
    /// </summary>
    [Fact]
    public void RetainedServiceCollection_ConfigurationFailure_AllowsModuleCollection()
    {
        var snapshot = CreateRetainedConfigurationFailure();

        snapshot.Services.Count.ShouldBeGreaterThan(0);
        ForceCollection();

        snapshot.Provider.TryGetTarget(out _).ShouldBeFalse();
        snapshot.Module.TryGetTarget(out _).ShouldBeFalse();
        GC.KeepAlive(snapshot.Services);
    }

    /// <summary>
    /// 验证 ValidateOnBuild 失败后外部保留服务集合不会继续保留已构造模块。
    /// </summary>
    [Fact]
    public void RetainedServiceCollection_ValidateOnBuildFailure_AllowsModuleCollection()
    {
        var snapshot = CreateRetainedValidateOnBuildFailure();

        snapshot.Services.Count.ShouldBeGreaterThan(0);
        ForceCollection();

        snapshot.Module.TryGetTarget(out _).ShouldBeFalse();
        GC.KeepAlive(snapshot.Services);
    }

    /// <summary>
    /// 验证旧入口保留服务集合时，构建器不会继续保留模块实例。
    /// </summary>
    [Fact]
    public void RetainedLegacyServiceCollection_NormalShutdown_AllowsModuleCollection()
    {
        var snapshot = CreateRetainedLegacyApplication();

        snapshot.Services.Count.ShouldBeGreaterThan(0);
        ForceCollection();

        snapshot.Provider.TryGetTarget(out _).ShouldBeFalse();
        snapshot.Module.TryGetTarget(out _).ShouldBeFalse();
        GC.KeepAlive(snapshot.Services);
    }

    /// <summary>
    /// 创建并构建指定模块的服务提供程序。
    /// </summary>
    /// <typeparam name="T">根模块类型。</typeparam>
    /// <returns>已构建的服务提供程序。</returns>
    private static ServiceProvider Create<T>() where T : BingModule => CreateServices<T>().BuildServiceProvider();

    /// <summary>
    /// 创建指定模块的服务集合。
    /// </summary>
    /// <typeparam name="T">根模块类型。</typeparam>
    /// <returns>已完成模块注册的服务集合。</returns>
    private static IServiceCollection CreateServices<T>() where T : BingModule =>
        new ServiceCollection().AddBingApplication<T>(options => options.AutoRegisterServices = false);

    /// <summary>
    /// 创建并关闭一个仍由调用方保留服务集合的正常应用。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference<BingModule> Module,
        WeakReference<ServiceProvider> Provider) CreateRetainedNormalApplication()
    {
        var services = new ServiceCollection();
        services.AddBingApplication<RetainedNormalModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        provider.UseBing();
        var module = provider.GetServices<BingModule>().OfType<RetainedNormalModule>().Single();
        provider.ShutdownBing();
        provider.Dispose();
        return (services, new WeakReference<BingModule>(module), new WeakReference<ServiceProvider>(provider));
    }

    /// <summary>
    /// 创建配置失败应用并保留服务集合，以便验证失败清理后的对象回收。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference<BingModule> Module,
        WeakReference<ServiceProvider> Provider) CreateRetainedConfigurationFailure()
    {
        RetainedConfigurationFailureModule.LastInstance = null;
        var services = new ServiceCollection();
        var error = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<RetainedConfigurationFailureModule>(options => options.AutoRegisterServices = false));
        error.Message.ShouldBe("retained configuration failure");
        RetainedConfigurationFailureModule.LastInstance.ShouldNotBeNull();

        var provider = services.BuildServiceProvider();
        provider.Dispose();
        return (services, RetainedConfigurationFailureModule.LastInstance,
            new WeakReference<ServiceProvider>(provider));
    }

    /// <summary>
    /// 创建 ValidateOnBuild 失败应用并保留服务集合，以便验证失败清理后的对象回收。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference<BingModule> Module) CreateRetainedValidateOnBuildFailure()
    {
        RetainedValidateOnBuildModule.LastInstance = null;
        var services = new ServiceCollection();
        services.AddBingApplication<RetainedValidateOnBuildModule>(options => options.AutoRegisterServices = false);
        services.AddSingleton<RetainedRequiresMissingService>();

        Should.Throw<Exception>(() => services.BuildBingServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        }));

        RetainedValidateOnBuildModule.LastInstance.ShouldNotBeNull();
        return (services, RetainedValidateOnBuildModule.LastInstance);
    }

    /// <summary>
    /// 创建并关闭一个仍由调用方保留服务集合的旧入口应用。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (IServiceCollection Services, WeakReference<BingModule> Module,
        WeakReference<ServiceProvider> Provider) CreateRetainedLegacyApplication()
    {
        var services = new ServiceCollection();
        var builder = services.AddBing();
        builder.AddModule<RetainedNormalModule>();
        var provider = services.BuildBingServiceProvider();
        provider.UseBing();
        var module = provider.GetServices<BingModule>().OfType<RetainedNormalModule>().Single();
        provider.ShutdownBing();
        builder.Modules.ShouldBeEmpty();
        provider.Dispose();
        return (services, new WeakReference<BingModule>(module), new WeakReference<ServiceProvider>(provider));
    }

    /// <summary>
    /// 重复执行完整 GC 周期，降低 JIT 和终结器时序对弱引用断言的影响。
    /// </summary>
    private static void ForceCollection()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    /// <summary>
    /// 记录相等模块实例的释放次数。
    /// </summary>
    private sealed class EqualResourceProbe
    {
        /// <summary>
        /// 获取或设置子模块同步释放次数。
        /// </summary>
        public static int ChildSyncDisposals;
        /// <summary>
        /// 获取或设置根模块同步释放次数。
        /// </summary>
        public static int RootSyncDisposals;
        /// <summary>
        /// 获取或设置子模块异步释放次数。
        /// </summary>
        public static int ChildAsyncDisposals;
        /// <summary>
        /// 获取或设置根模块异步释放次数。
        /// </summary>
        public static int RootAsyncDisposals;

        /// <summary>
        /// 重置所有释放计数。
        /// </summary>
        public static void Reset()
        {
            ChildSyncDisposals = 0;
            RootSyncDisposals = 0;
            ChildAsyncDisposals = 0;
            RootAsyncDisposals = 0;
        }
    }

    /// <summary>
    /// 记录纯异步资源释放路径的次数。
    /// </summary>
    private sealed class AsyncOnlyProbe
    {
        /// <summary>
        /// 获取或设置关闭次数。
        /// </summary>
        public static int Shutdowns;
        /// <summary>
        /// 获取或设置释放次数。
        /// </summary>
        public static int Disposals;
        /// <summary>
        /// 获取或设置配置失败根模块释放次数。
        /// </summary>
        public static int ConfigurationRootDisposals;
        /// <summary>
        /// 获取或设置配置失败依赖模块释放次数。
        /// </summary>
        public static int ConfigurationDependencyDisposals;
        /// <summary>
        /// 获取或设置构造失败模块释放次数。
        /// </summary>
        public static int ConstructorDisposals;
        /// <summary>
        /// 获取或设置上下文捕获模块释放次数。
        /// </summary>
        public static int ContextDisposals;

        /// <summary>
        /// 重置所有异步资源计数。
        /// </summary>
        public static void Reset()
        {
            Shutdowns = 0;
            Disposals = 0;
            ConfigurationRootDisposals = 0;
            ConfigurationDependencyDisposals = 0;
            ConstructorDisposals = 0;
            ContextDisposals = 0;
        }
    }

    /// <summary>
    /// 不执行回调的同步上下文，用于检测同步阻塞。
    /// </summary>
    private sealed class BlockingSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc />
        public override void Post(SendOrPostCallback d, object state) { }
        /// <inheritdoc />
        public override void Send(SendOrPostCallback d, object state) { }
    }

    /// <summary>
    /// 使不同模块实例在相等性比较中相等的基类。
    /// </summary>
    private abstract class EqualModuleBase : BingModule
    {
        /// <inheritdoc />
        public override bool Equals(object obj) => obj is EqualModuleBase;
        /// <inheritdoc />
        public override int GetHashCode() => 17;
    }

    /// <summary>
    /// 同时支持同步和异步释放的模块基类。
    /// </summary>
    private abstract class EqualAsyncModuleBase : EqualModuleBase, IDisposable, IAsyncDisposable
    {
        /// <inheritdoc />
        public abstract void Dispose();
        /// <inheritdoc />
        public abstract ValueTask DisposeAsync();
    }

    /// <summary>
    /// 记录根模块同步释放的测试模块。
    /// </summary>
    [DependsOnModule(typeof(EqualChildModule))]
    private sealed class EqualRootModule : EqualModuleBase, IDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualRootModule() { }
        /// <inheritdoc />
        public void Dispose() => EqualResourceProbe.RootSyncDisposals++;
    }

    /// <summary>
    /// 记录子模块同步释放的测试模块。
    /// </summary>
    private sealed class EqualChildModule : EqualModuleBase, IDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualChildModule() { }
        /// <inheritdoc />
        public void Dispose() => EqualResourceProbe.ChildSyncDisposals++;
    }

    /// <summary>
    /// 记录根模块异步释放的测试模块。
    /// </summary>
    [DependsOnModule(typeof(EqualAsyncChildModule))]
    private sealed class EqualAsyncRootModule : EqualAsyncModuleBase
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualAsyncRootModule() { }
        /// <inheritdoc />
        public override void Dispose() => EqualResourceProbe.RootSyncDisposals++;
        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            EqualResourceProbe.RootAsyncDisposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 记录子模块异步释放的测试模块。
    /// </summary>
    private sealed class EqualAsyncChildModule : EqualAsyncModuleBase
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualAsyncChildModule() { }
        /// <inheritdoc />
        public override void Dispose() => EqualResourceProbe.ChildSyncDisposals++;
        /// <inheritdoc />
        public override ValueTask DisposeAsync()
        {
            EqualResourceProbe.ChildAsyncDisposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 在配置阶段失败的根模块。
    /// </summary>
    [DependsOnModule(typeof(EqualFailingChildModule))]
    private sealed class EqualFailingRootModule : EqualModuleBase, IDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualFailingRootModule() { }
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services) =>
            throw new InvalidOperationException("intentional configuration failure");

        /// <inheritdoc />
        public void Dispose() => EqualResourceProbe.RootSyncDisposals++;
    }

    /// <summary>
    /// 配置失败根模块的依赖模块。
    /// </summary>
    private sealed class EqualFailingChildModule : EqualModuleBase, IDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public EqualFailingChildModule() { }
        /// <inheritdoc />
        public void Dispose() => EqualResourceProbe.ChildSyncDisposals++;
    }

    /// <summary>
    /// 记录构造失败场景释放次数的状态。
    /// </summary>
    private sealed class ConstructionProbe
    {
        /// <summary>
        /// 获取或设置根模块释放次数。
        /// </summary>
        public static int RootDisposals;

        /// <summary>
        /// 重置释放次数。
        /// </summary>
        public static void Reset() => RootDisposals = 0;
    }

    /// <summary>
    /// 依赖构造失败的根模块。
    /// </summary>
    [DependsOnModule(typeof(ThrowingConstructorDependencyModule))]
    private sealed class ThrowingConstructorRootModule : BingModule, IDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public ThrowingConstructorRootModule() { }
        /// <inheritdoc />
        public void Dispose() => ConstructionProbe.RootDisposals++;
    }

    /// <summary>
    /// 构造函数抛出异常的依赖模块。
    /// </summary>
    private sealed class ThrowingConstructorDependencyModule : BingModule
    {
        /// <summary>
        /// 初始化并抛出测试异常。
        /// </summary>
        public ThrowingConstructorDependencyModule() => throw new InvalidOperationException("intentional constructor failure");
    }

    /// <summary>
    /// 记录关闭取消过程的状态。
    /// </summary>
    private sealed class ShutdownCancellationProbe
    {
        /// <summary>
        /// 获取关闭钩子尝试顺序。
        /// </summary>
        public List<string> HookAttempts { get; } = new();
        /// <summary>
        /// 获取同步释放顺序。
        /// </summary>
        public List<string> SyncDisposals { get; } = new();
        /// <summary>
        /// 获取第一个关闭钩子进入通知。
        /// </summary>
        public TaskCompletionSource<bool> FirstHookEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 获取释放第一个关闭钩子的通知。
        /// </summary>
        public TaskCompletionSource<bool> ReleaseFirstHook { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 在关闭钩子中响应取消并记录释放顺序的模块基类。
    /// </summary>
    private abstract class CancellationShutdownModule : BingModule, IBingAsyncModuleShutdown, IDisposable
    {
        /// <summary>
        /// 获取或设置共享关闭测试状态。
        /// </summary>
        public static ShutdownCancellationProbe Probe { get; set; }

        /// <inheritdoc />
        public async Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            var probe = Probe;
            var name = GetType().Name;
            probe.HookAttempts.Add(name);
            if (name == nameof(CancellationRootModule))
            {
                probe.FirstHookEntered.TrySetResult(true);
                await probe.ReleaseFirstHook.Task.WaitAsync(cancellationToken);
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <inheritdoc />
        public void Dispose() => Probe.SyncDisposals.Add(GetType().Name);
    }

    /// <summary>
    /// 关闭取消测试的根模块。
    /// </summary>
    [DependsOnModule(typeof(CancellationChildModule))]
    private sealed class CancellationRootModule : CancellationShutdownModule
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public CancellationRootModule() { }
    }

    /// <summary>
    /// 关闭取消测试的依赖模块。
    /// </summary>
    private sealed class CancellationChildModule : CancellationShutdownModule
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public CancellationChildModule() { }
    }

    /// <summary>
    /// 仅提供异步关闭和异步释放的模块。
    /// </summary>
    private sealed class AsyncOnlyModule : BingModule, IBingAsyncModuleShutdown, IAsyncDisposable
    {
        /// <summary>
        /// 初始化测试模块。
        /// </summary>
        public AsyncOnlyModule() { }
        /// <inheritdoc />
        public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            AsyncOnlyProbe.Shutdowns++;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncOnlyProbe.Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 在配置阶段失败并异步释放资源的根模块。
    /// </summary>
    [DependsOnModule(typeof(AsyncOnlyConfigurationDependencyModule))]
    private sealed class AsyncOnlyConfigurationFailureRootModule : BingModule, IAsyncDisposable
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services) =>
            throw new InvalidOperationException("intentional async-only configuration failure");

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncOnlyProbe.ConfigurationRootDisposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 配置失败测试的异步释放依赖模块。
    /// </summary>
    private sealed class AsyncOnlyConfigurationDependencyModule : BingModule, IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncOnlyProbe.ConfigurationDependencyDisposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 构造失败测试的异步释放根模块。
    /// </summary>
    [DependsOnModule(typeof(ThrowingConstructorDependencyModule))]
    private sealed class AsyncOnlyConstructorRootModule : BingModule, IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncOnlyProbe.ConstructorDisposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 验证异步释放不会捕获同步上下文的模块。
    /// </summary>
    private sealed class ContextCapturingAsyncOnlyConfigurationModule : BingModule, IAsyncDisposable
    {
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services) =>
            throw new InvalidOperationException("intentional context capturing configuration failure");

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await Task.Yield();
            AsyncOnlyProbe.ContextDisposals++;
        }
    }

    /// <summary>
    /// 正常关闭回收测试模块。
    /// </summary>
    private sealed class RetainedNormalModule : BingModule, IDisposable
    {
        /// <summary>
        /// 初始化模块。
        /// </summary>
        public RetainedNormalModule() { }

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 配置失败回收测试模块。
    /// </summary>
    private sealed class RetainedConfigurationFailureModule : BingModule, IDisposable
    {
        /// <summary>
        /// 最近构造实例的弱引用。
        /// </summary>
        public static WeakReference<BingModule> LastInstance { get; set; }

        /// <summary>
        /// 初始化模块并记录弱引用。
        /// </summary>
        public RetainedConfigurationFailureModule() => LastInstance = new WeakReference<BingModule>(this);

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services) =>
            throw new InvalidOperationException("retained configuration failure");

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// ValidateOnBuild 回收测试模块。
    /// </summary>
    private sealed class RetainedValidateOnBuildModule : BingModule, IDisposable
    {
        /// <summary>
        /// 最近构造实例的弱引用。
        /// </summary>
        public static WeakReference<BingModule> LastInstance { get; set; }

        /// <summary>
        /// 初始化模块并记录弱引用。
        /// </summary>
        public RetainedValidateOnBuildModule() => LastInstance = new WeakReference<BingModule>(this);

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 用于触发 ValidateOnBuild 失败的服务。
    /// </summary>
    private sealed class RetainedRequiresMissingService
    {
        /// <summary>
        /// 初始化并请求未注册依赖。
        /// </summary>
        /// <param name="dependency">故意未注册的依赖。</param>
        public RetainedRequiresMissingService(RetainedMissingService dependency) { }
    }

    /// <summary>
    /// ValidateOnBuild 缺失依赖标记。
    /// </summary>
    private sealed class RetainedMissingService { }
}
