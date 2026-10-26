using System.Reflection;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证模块生命周期只解析一次 scope 工厂，并直接观测真实生命周期的作用域所有权。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleLifecycleRetentionTest
{
    /// <summary>
    /// 正常初始化与显式关闭应各自拥有一个独立 scope，并复用同一个工厂。
    /// </summary>
    [Fact]
    public void NormalLifecycle_ShouldOwnInitAndShutdownScopes_AndReuseFactory()
    {
        var module = new RecordingLifecycleModule();
        var observation = LifecycleObservation.Create(module);

        observation.Manager.Initialize();

        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Single(observation.Factory.Scopes);
        Assert.Equal(1, observation.Factory.Scopes[0].DisposeCount);
        Assert.Equal("initialize", Assert.Single(module.Events));

        observation.Manager.Shutdown();

        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Equal(2, observation.Factory.Scopes.Count);
        Assert.NotSame(observation.Factory.Scopes[0], observation.Factory.Scopes[1]);
        Assert.All(observation.Factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.Equal(new[] { "initialize", "shutdown" }, module.Events);

        observation.Manager.Shutdown();
        ((IDisposable)observation.Manager).Dispose();

        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Equal(2, observation.Factory.Scopes.Count);
        Assert.All(observation.Factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.Equal(new[] { "initialize", "shutdown" }, module.Events);
    }

    /// <summary>
    /// 初始化钩子失败时，初始化 scope 与失败清理 scope 应各释放一次，并保留原异常。
    /// </summary>
    [Fact]
    public void InitializationHookFailure_ShouldDisposeInitAndCleanupScopesOnce()
    {
        var module = new FailingLifecycleModule();
        var observation = LifecycleObservation.Create(module);

        var error = Assert.Throws<InvalidOperationException>(() => observation.Manager.Initialize());

        Assert.Equal("hook failure", error.Message);
        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Equal(2, observation.Factory.Scopes.Count);
        Assert.All(observation.Factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.Equal(new[] { "initialize", "shutdown" }, module.Events);
        Assert.Equal(new[] { observation.Factory.Scopes[1], observation.Factory.Scopes[0] },
            observation.Factory.DisposeSequence);
    }

    /// <summary>
    /// 进入异步初始化后由信号取消，应保留 OCE 并释放初始化与清理 scope 各一次。
    /// </summary>
    [Fact]
    public async Task CancellationAfterEnteringInitialization_ShouldDisposeInitAndCleanupScopesOnce()
    {
        var module = new SignalingAsyncLifecycleModule();
        var observation = LifecycleObservation.Create(module);
        using var cancellation = new CancellationTokenSource();

        var initialization = observation.Manager.InitializeAsync(cancellationToken: cancellation.Token);
        await module.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);

        Assert.True(error.CancellationToken.IsCancellationRequested);
        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Equal(2, observation.Factory.Scopes.Count);
        Assert.All(observation.Factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
        Assert.Equal(new[] { "initialize", "shutdown" }, module.Events);
        Assert.Equal(new[] { observation.Factory.Scopes[1], observation.Factory.Scopes[0] },
            observation.Factory.DisposeSequence);
    }

    /// <summary>
    /// 已完成初始化后 provider Dispose 不得再创建关闭 scope。
    /// </summary>
    [Fact]
    public void ProviderDisposeAfterInitialization_ShouldNotCreateShutdownScope()
    {
        var module = new RecordingLifecycleModule();
        var observation = LifecycleObservation.Create(module);

        observation.Manager.Initialize();
        ((IDisposable)observation.Manager).Dispose();

        Assert.Equal(1, observation.Factory.ResolveCount);
        Assert.Single(observation.Factory.Scopes);
        Assert.Equal(1, observation.Factory.Scopes[0].DisposeCount);
        Assert.Equal(new[] { "initialize", "shutdown" }, module.Events);
    }

    /// <summary>
    /// Create helper 仍应返回调用方传入的同一个 Recorder 实例。
    /// </summary>
    [Fact]
    public void CreateHelper_ShouldReturnSameRecorderInstance_FromSingletonFactory()
    {
        var recorder = new ModuleLifecycleTest.Recorder();
        using var provider = Create<ModuleLifecycleTest.ScopedModule>(recorder);

        Assert.Same(recorder, provider.GetRequiredService<ModuleLifecycleTest.Recorder>());
    }

    /// <summary>
    /// 私有 helper 只验证缓存字段和 scope 创建顺序；真实生命周期所有权由上面的用例覆盖。
    /// </summary>
    [Fact]
    public void PrivateHelperContract_ShouldResolveFactoryOnce_AndCreateDistinctDisposableScopes()
    {
        var provider = new ObservedServiceProvider();
        var managerType = typeof(IBingModuleManager).Assembly.GetType("Bing.Core.Modularity.BingModuleManager",
            throwOnError: true);
        var registrationType = managerType.Assembly.GetType("Bing.Core.Modularity.BingModuleRegistration",
            throwOnError: true);
        var registration = Activator.CreateInstance(registrationType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { new ServiceCollection(), false, false }, null);
        var manager = Activator.CreateInstance(managerType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { (object)provider, registration }, null);
        var createMethod = managerType.GetMethod("CreateLifecycleScope",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var first = (IServiceScope)createMethod.Invoke(manager, null);
        var second = (IServiceScope)createMethod.Invoke(manager, null);

        Assert.Equal(1, provider.Factory.ResolveCount);
        Assert.Equal(2, provider.Factory.Scopes.Count);
        Assert.NotSame(first, second);
        first.Dispose();
        second.Dispose();
        Assert.All(provider.Factory.Scopes, scope => Assert.Equal(1, scope.DisposeCount));
    }

    /// <summary>
    /// 创建返回指定记录器实例的测试服务提供程序。
    /// </summary>
    /// <typeparam name="T">参与构建的模块类型。</typeparam>
    /// <param name="recorder">期望由容器返回的记录器实例。</param>
    /// <returns>启用作用域校验的服务提供程序。</returns>
    private static ServiceProvider Create<T>(ModuleLifecycleTest.Recorder recorder) where T : BingModule
    {
        var services = new ServiceCollection();
        var recorderInstance = recorder ?? new ModuleLifecycleTest.Recorder();
        services.AddSingleton<ModuleLifecycleTest.Recorder>(_ => recorderInstance);
        services.AddScoped<ModuleLifecycleTest.ScopedResource>();
        services.AddBingApplication<T>(options => options.AutoRegisterServices = false);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// 记录真实生命周期事件的基础模块。
    /// </summary>
    private class RecordingLifecycleModule : BingModule
    {
        /// <summary>
        /// 获取生命周期事件。
        /// </summary>
        public List<string> Events { get; } = new();

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            Events.Add("initialize");

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            Events.Add("shutdown");
    }

    /// <summary>
    /// 在初始化钩子中抛出原始异常的模块。
    /// </summary>
    private sealed class FailingLifecycleModule : RecordingLifecycleModule
    {
        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context)
        {
            base.OnApplicationInitialization(context);
            throw new InvalidOperationException("hook failure");
        }
    }

    /// <summary>
    /// 进入异步初始化后等待取消信号的模块。
    /// </summary>
    private sealed class SignalingAsyncLifecycleModule : RecordingLifecycleModule
    {
        /// <summary>
        /// 获取模块已进入异步初始化的通知。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 永不完成的异步信号，用于让模块停留在初始化阶段。
        /// </summary>
        private readonly TaskCompletionSource<bool> _never = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public override async Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            Events.Add("initialize");
            Entered.TrySetResult(true);
            await _never.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 通过真实 manager 生命周期共享 factory/scope 观测。
    /// </summary>
    private sealed class LifecycleObservation
    {
        /// <summary>
        /// 初始化一个 <see cref="LifecycleObservation" /> 类的新实例。
        /// </summary>
        /// <param name="provider">自定义根服务提供程序。</param>
        /// <param name="manager">被测试的真实模块管理器。</param>
        private LifecycleObservation(ObservedServiceProvider provider, IBingModuleManager manager)
        {
            Provider = provider;
            Manager = manager;
        }

        /// <summary>
        /// 获取自定义根 provider。
        /// </summary>
        public ObservedServiceProvider Provider { get; }

        /// <summary>
        /// 获取被测试的真实 manager。
        /// </summary>
        public IBingModuleManager Manager { get; }

        /// <summary>
        /// 获取自定义 scope 工厂。
        /// </summary>
        public ObservedScopeFactory Factory => Provider.Factory;

        /// <summary>
        /// 为单个模块构造真实 manager，不通过生产容器替换 DI 内置工厂。
        /// </summary>
        /// <param name="module">要注入模块注册记录的测试模块。</param>
        /// <returns>共享同一观测 provider 的生命周期观测上下文。</returns>
        public static LifecycleObservation Create(BingModule module)
        {
            var provider = new ObservedServiceProvider();
            var managerType = typeof(IBingModuleManager).Assembly.GetType("Bing.Core.Modularity.BingModuleManager",
                throwOnError: true);
            var registrationType = managerType.Assembly.GetType("Bing.Core.Modularity.BingModuleRegistration",
                throwOnError: true);
            var registration = Activator.CreateInstance(registrationType,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { new ServiceCollection(), false, false }, null);
            registrationType.GetMethod("SetModules", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(registration, new object[] { new[] { new BingModuleDescriptor(module, Array.Empty<Type>(), 0) } });
            var manager = Activator.CreateInstance(managerType,
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { (object)provider, registration }, null);
            return new LifecycleObservation(provider, (IBingModuleManager)manager);
        }
    }

    /// <summary>
    /// 记录 factory 解析次数、scope 创建和释放顺序的自定义 provider。
    /// </summary>
    private sealed class ObservedServiceProvider : IServiceProvider
    {
        /// <summary>
        /// 获取共享 factory。
        /// </summary>
        public ObservedScopeFactory Factory { get; } = new();

        /// <inheritdoc />
        public object GetService(Type serviceType)
        {
            if (serviceType != typeof(IServiceScopeFactory))
                return null;
            Factory.ResolveCount++;
            return Factory;
        }
    }

    /// <summary>
    /// 记录 scope 身份和释放顺序的自定义工厂。
    /// </summary>
    private sealed class ObservedScopeFactory : IServiceScopeFactory
    {
        /// <summary>
        /// 获取或设置 factory 被解析的次数。
        /// </summary>
        public int ResolveCount { get; set; }

        /// <summary>
        /// 获取已创建 scope。
        /// </summary>
        public List<ObservedScope> Scopes { get; } = new();

        /// <summary>
        /// 获取按实际释放顺序保存的 scope。
        /// </summary>
        public List<ObservedScope> DisposeSequence { get; } = new();

        /// <inheritdoc />
        public IServiceScope CreateScope()
        {
            var scope = new ObservedScope(this);
            Scopes.Add(scope);
            return scope;
        }

        /// <summary>
        /// 记录一次 scope 释放。
        /// </summary>
        /// <param name="scope">已释放的 scope。</param>
        public void RecordDispose(ObservedScope scope) => DisposeSequence.Add(scope);
    }

    /// <summary>
    /// 可观测释放次数的 scope。
    /// </summary>
    private sealed class ObservedScope : IServiceScope
    {
        /// <summary>
        /// 初始化一个 <see cref="ObservedScope" /> 类的新实例。
        /// </summary>
        /// <param name="factory">创建并记录该 scope 的工厂。</param>
        public ObservedScope(ObservedScopeFactory factory) => ServiceProvider = new EmptyServiceProvider(factory);

        /// <inheritdoc />
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeCount++;
            ((EmptyServiceProvider)ServiceProvider).Factory.RecordDispose(this);
        }
    }

    /// <summary>
    /// 允许生命周期观测但不提供额外服务的 scope provider。
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="EmptyServiceProvider" /> 类的新实例。
        /// </summary>
        /// <param name="factory">所属的观测 scope 工厂。</param>
        public EmptyServiceProvider(ObservedScopeFactory factory) => Factory = factory;

        /// <summary>
        /// 获取所属工厂。
        /// </summary>
        public ObservedScopeFactory Factory { get; }

        /// <inheritdoc />
        public object GetService(Type serviceType) => null;
    }
}
