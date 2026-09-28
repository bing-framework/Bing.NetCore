using System.Runtime.CompilerServices;
using Bing.Core.Modularity;
using Bing.DependencyInjection;
using Bing.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Tests.Modularity;

/// <summary>
/// 禁用模块静态兼容入口并行执行的测试集合定义。
/// </summary>
[CollectionDefinition("Module static compatibility", DisableParallelization = true)]
public class ModuleStaticCompatibilityCollection { }

/// <summary>
/// 验证模块初始化、关闭和资源清理生命周期。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleLifecycleTest
{
    /// <summary>
    /// 创建启用作用域校验的测试服务提供程序。
    /// </summary>
    /// <typeparam name="T">根模块类型。</typeparam>
    /// <param name="recorder">可选的事件记录器。</param>
    /// <returns>已构建的服务提供程序。</returns>
    private static ServiceProvider Create<T>(Recorder recorder = null) where T : BingModule
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder ?? new Recorder());
        services.AddScoped<ScopedResource>();
        services.AddBingApplication<T>(o => o.AutoRegisterServices = false);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// 验证初始化只执行一次并按逆序关闭。
    /// </summary>
    [Fact]
    public void InitializesOnce_AndStopsInReverseOrder()
    {
        var recorder = new Recorder();
        using var provider = Create<ParentModule>(recorder);
        provider.UseBing();
        provider.UseBing();
        provider.ShutdownBing();
        provider.ShutdownBing();
        recorder.Events.ShouldBe(new[] { "start-child", "start-parent", "stop-parent", "stop-child", "dispose-parent", "dispose-child" });
        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
    }

    /// <summary>
    /// 验证根容器重复释放不会重复释放模块。
    /// </summary>
    [Fact]
    public void ProviderDispose_ReleasesOwnedModulesOnce()
    {
        var recorder = new Recorder();
        var provider = Create<ParentModule>(recorder);
        provider.UseBing();
        provider.Dispose();
        provider.Dispose();
        recorder.Events.Count(e => e.StartsWith("stop-")).ShouldBe(2);
        recorder.Events.Count(e => e.StartsWith("dispose-")).ShouldBe(2);
    }

    /// <summary>
    /// 验证初始化失败会清理已启动模块并保留原始异常。
    /// </summary>
    [Fact]
    public void InitializationFailure_CleansStartedAndFailingModule_PreservesError()
    {
        var recorder = new Recorder();
        using var provider = Create<FailingModule>(recorder);
        var error = Assert.Throws<InvalidOperationException>(() => provider.UseBing());
        error.Message.ShouldBe("initialization failure");
        error.Data["Bing.ModulePhase"].ShouldBe("Initialize");
        recorder.Events.ShouldBe(new[] { "start-child", "start-parent", "stop-parent", "stop-child", "dispose-parent", "dispose-child" });
        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
    }

    /// <summary>
    /// 验证关闭异常不会阻止剩余模块清理。
    /// </summary>
    [Fact]
    public void ShutdownError_DoesNotPreventRemainingCleanup()
    {
        var recorder = new Recorder();
        using var provider = Create<FailingShutdownModule>(recorder);
        provider.UseBing();
        var error = Assert.Throws<AggregateException>(() => provider.ShutdownBing());
        error.InnerExceptions.Count.ShouldBe(1);
        recorder.Events.ShouldContain("stop-child");
        recorder.Events.ShouldContain("dispose-parent");
        recorder.Events.ShouldContain("dispose-child");
        provider.ShutdownBing();
    }

    /// <summary>
    /// 验证异步初始化会等待完成且不会重复执行同步钩子。
    /// </summary>
    [Fact]
    public async Task AsyncInitializer_IsAwaited_AndNotAlsoCalledSynchronously()
    {
        var recorder = new Recorder();
        await using var provider = Create<AsyncModule>(recorder);
        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
        var manager = provider.GetRequiredService<IBingModuleManager>();
        var first = manager.InitializeAsync();
        first.IsCompleted.ShouldBeFalse();
        var second = manager.InitializeAsync();
        ReferenceEquals(first, second).ShouldBeTrue();
        recorder.Gate.SetResult(true);
        await first;
        await second;
        recorder.Events.ShouldBe(new[] { "async-start", "async-ready" });
        recorder.Scoped.Disposed.ShouldBeTrue();
        await provider.ShutdownBingAsync();
        recorder.Events.ShouldContain("async-stop");
    }

    /// <summary>
    /// 验证初始化取消会清理部分状态并禁止隐式重试。
    /// </summary>
    [Fact]
    public async Task Cancellation_CleansPartialInitialization_WithoutRetry()
    {
        var recorder = new Recorder();
        await using var provider = Create<AsyncModule>(recorder);
        using var cancellation = new CancellationTokenSource();
        var pending = provider.UseBingAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        recorder.Events.ShouldBe(new[] { "async-start", "async-stop" });
        recorder.Scoped.Disposed.ShouldBeTrue();
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.UseBingAsync());
    }

    /// <summary>
    /// 验证预取消初始化不执行钩子且之后可以重新开始。
    /// </summary>
    [Fact]
    public async Task PreCanceledInitialization_DoesNotRunHooks_AndCanStartLater()
    {
        var recorder = new Recorder();
        await using var provider = Create<AsyncModule>(recorder);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.UseBingAsync(new CancellationToken(true)));
        recorder.Events.ShouldBeEmpty();
        recorder.Gate.SetResult(true);
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
    }

    /// <summary>
    /// 验证生命周期重入会快速失败而不会死锁。
    /// </summary>
    [Fact]
    public async Task Reentry_IsRejectedWithoutDeadlocking()
    {
        await using var provider = Create<ReentrantModule>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.UseBingAsync());
        error.Message.ShouldContain("重入");
    }

    /// <summary>
    /// 验证初始化作用域在成功返回前已释放。
    /// </summary>
    [Fact]
    public void InitializationScope_IsDisposed_BeforeSuccessfulReturn()
    {
        var recorder = new Recorder();
        using var provider = Create<ScopedModule>(recorder);
        provider.UseBing();
        recorder.Scoped.Disposed.ShouldBeTrue();
        provider.ShutdownBing();
    }

    /// <summary>
    /// 验证纯异步模块使用异步生命周期且只释放一次。
    /// </summary>
    [Fact]
    public async Task AsyncDisposableOnlyModule_UsesAsyncLifecycle_AndDisposesOnce()
    {
        var recorder = new Recorder();
        await using var provider = Create<AsyncOnlyLifecycleModule>(recorder);
        await provider.UseBingAsync();
        Assert.Throws<InvalidOperationException>(() => provider.ShutdownBing());
        await provider.ShutdownBingAsync();
        await provider.ShutdownBingAsync();
        recorder.Events.ShouldBe(new[] { "async-only-stop", "async-only-dispose" });
    }

    /// <summary>
    /// 验证异步关闭会停止后台任务并只退订一次事件。
    /// </summary>
    [Fact]
    public async Task AsyncShutdown_CancelsBackgroundLoopAndUnsubscribesExactlyOnce()
    {
        var recorder = new Recorder();
        await using var provider = Create<AsyncResourceModule>(recorder);
        await provider.UseBingAsync();

        var source = provider.GetRequiredService<TestEventSource>();
        var state = provider.GetRequiredService<AsyncResourceState>();
        await state.Started.Task;

        source.Publish();
        state.EventCallbackCount.ShouldBe(1);

        await provider.ShutdownBingAsync();

        state.Completed.Task.IsCompletedSuccessfully.ShouldBeTrue();
        state.ShutdownCount.ShouldBe(1);
        source.Publish();
        state.EventCallbackCount.ShouldBe(1);

        await provider.ShutdownBingAsync();
        state.ShutdownCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证新入口不绑定全局服务定位器且多个应用相互隔离。
    /// </summary>
    [Fact]
    public void NewApplications_DoNotBindGlobalServiceLocator_AndRemainIsolated()
    {
        var wasEnabled = ServiceLocator.Instance.IsProviderEnabled;
        var first = new Recorder();
        var second = new Recorder();
        using var a = Create<ScopedModule>(first);
        using var b = Create<ScopedModule>(second);
        a.UseBing();
        b.UseBing();
        first.Scoped.ShouldNotBeSameAs(second.Scoped);
        ServiceLocator.Instance.IsProviderEnabled.ShouldBe(wasEnabled);
        a.ShutdownBing();
        b.ShutdownBing();
    }

    /// <summary>
    /// 验证停止旧兼容宿主不会解绑新宿主。
    /// </summary>
    [Fact]
    public void StoppingOlderLegacyHost_DoesNotUnbindNewerHost()
    {
        var a = new ServiceCollection();
        var b = new ServiceCollection();
        var first = new Recorder();
        var second = new Recorder();
        a.AddSingleton(first);
        b.AddSingleton(second);
        a.AddBing();
        b.AddBing();
        using var pa = a.BuildServiceProvider();
        using var pb = b.BuildServiceProvider();
        pa.UseBing();
        pb.UseBing();
        pa.ShutdownBing();
        ServiceLocator.Instance.GetService<Recorder>().ShouldBeSameAs(second);
        pb.ShutdownBing();
        ServiceLocator.Instance.IsProviderEnabled.ShouldBeFalse();
        ServiceLocator.Instance.ScopedProvider.ShouldBeNull();
        ServiceLocator.InScoped().ShouldBeFalse();
        ServiceLocator.Instance.GetService<Recorder>().ShouldBeNull();
        ServiceLocator.Instance.GetService(typeof(Recorder)).ShouldBeNull();
        ServiceLocator.Instance.GetServices<Recorder>().ShouldBeEmpty();
        ServiceLocator.Instance.GetServices(typeof(Recorder)).ShouldBeEmpty();
        ServiceLocator.Instance.GetServiceDescriptors().ShouldBeEmpty();
    }

    /// <summary>
    /// 验证启动日志输出后清空缓存。
    /// </summary>
    [Fact]
    public void StartupLogFlush_ClearsCachedEntries()
    {
        using var provider = Create<ScopedModule>();
        var logger = provider.GetRequiredService<StartupLogger>();
        logger.LogInformation("initialization", "module-test");
        provider.UseBing();
        logger.LogInfos.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证启动日志输出失败时保留原始初始化异常、清空日志缓存并完成模块清理。
    /// </summary>
    [Fact]
    public void StartupLogFlushFailure_PreservesInitializationError_CleansModuleAndPreventsRetry()
    {
        var recorder = new Recorder();
        var loggingError = new InvalidOperationException("startup logging failure");
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddScoped<ScopedResource>();
        services.AddBingApplication<LoggingFailureModule>(o => o.AutoRegisterServices = false);
        services.AddSingleton<ILoggerFactory>(new ThrowingLoggerFactory(loggingError));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var startupLogger = provider.GetRequiredService<StartupLogger>();
        startupLogger.LogInformation("queued startup log", "module-test");

        var error = Assert.Throws<InvalidOperationException>(() => provider.UseBing());

        error.Message.ShouldBe("original logging initialization failure");
        error.Data["Bing.ModulePhase"].ShouldBe("Initialize");
        error.Data["Bing.ModuleLogError"].ShouldBeSameAs(loggingError);
        startupLogger.LogInfos.ShouldBeEmpty();
        recorder.Events.ShouldBe(new[] { "logging-start", "logging-dispose" });
        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
        recorder.Events.ShouldBe(new[] { "logging-start", "logging-dispose" });
    }

    /// <summary>
    /// 验证初始化期间释放提供程序会取消并清理模块。
    /// </summary>
    [Fact]
    public async Task DisposingProviderDuringAsyncInitialization_CancelsAndCleansModules()
    {
        var recorder = new Recorder();
        var provider = Create<AsyncModule>(recorder);
        var initialization = provider.UseBingAsync();
        await provider.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        recorder.Events.ShouldBe(new[] { "async-start", "async-stop" });
        recorder.Scoped.Disposed.ShouldBeTrue();
    }

    /// <summary>
    /// 验证作用域释放异常不会覆盖原始初始化异常。
    /// </summary>
    [Fact]
    public void ScopeDisposalError_PreservesInitializationFailureAndCleansModule()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddScoped<ThrowingScopedResource>();
        services.AddBingApplication<ScopeFailureModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();
        var error = Assert.Throws<InvalidOperationException>(() => provider.UseBing());
        error.Message.ShouldBe("original startup error");
        ((AggregateException)error.Data["Bing.ModuleCleanupErrors"]).InnerExceptions
            .ShouldContain(e => e.Message == "scope disposal error");
        recorder.Events.ShouldContain("scope-module-disposed");
        Assert.Throws<InvalidOperationException>(() => provider.UseBing());
    }

    /// <summary>
    /// 验证异步释放模块可由提供程序异步释放。
    /// </summary>
    [Fact]
    public async Task AsyncDisposableOnlyModule_ProviderDisposeAsync_ReleasesModule()
    {
        var recorder = new Recorder();
        var provider = Create<AsyncOnlyLifecycleModule>(recorder);
        await provider.UseBingAsync();
        await provider.DisposeAsync();
        recorder.Events.ShouldBe(new[] { "async-only-stop", "async-only-dispose" });
    }

    /// <summary>
    /// 验证重复关闭的应用不会保留提供程序或模块。
    /// </summary>
    [Fact]
    public void RepeatedClosedApplications_DoNotKeepProvidersOrModulesAlive()
    {
        var references = Enumerable.Range(0, 12).SelectMany(_ => CreateClosedApplication()).ToArray();
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        references.All(r => !r.IsAlive).ShouldBeTrue();
    }

    /// <summary>
    /// 创建并关闭应用后返回其弱引用。
    /// </summary>
    /// <returns>提供程序、模块和记录器的弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateClosedApplication()
    {
        var recorder = new Recorder();
        using var provider = Create<ParentModule>(recorder);
        provider.UseBing();
        var module = provider.GetServices<BingModule>().OfType<ParentModule>().Single();
        provider.ShutdownBing();
        return new[] { new WeakReference(provider), new WeakReference(module), new WeakReference(recorder) };
    }

    /// <summary>
    /// 记录模块生命周期事件和测试作用域资源。
    /// </summary>
    public sealed class Recorder
    {
        /// <summary>
        /// 获取生命周期事件列表。
        /// </summary>
        public List<string> Events { get; } = new();
        /// <summary>
        /// 获取异步初始化闸门。
        /// </summary>
        public TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 获取或设置测试作用域资源。
        /// </summary>
        public ScopedResource Scoped { get; set; }
    }

    /// <summary>
    /// 可发布事件的测试事件源。
    /// </summary>
    public sealed class TestEventSource
    {
        /// <summary>
        /// 发布测试事件。
        /// </summary>
        public event Action Published;

        /// <summary>
        /// 触发已订阅的测试事件。
        /// </summary>
        public void Publish() => Published?.Invoke();
    }

    /// <summary>
    /// 保存异步资源模块运行状态。
    /// </summary>
    public sealed class AsyncResourceState
    {
        /// <summary>
        /// 获取后台任务已启动通知。
        /// </summary>
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 获取后台任务已完成通知。
        /// </summary>
        public TaskCompletionSource<bool> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 获取或设置事件回调次数。
        /// </summary>
        public int EventCallbackCount { get; set; }
        /// <summary>
        /// 获取或设置关闭次数。
        /// </summary>
        public int ShutdownCount { get; set; }
        /// <summary>
        /// 获取停止后台任务的通知。
        /// </summary>
        public TaskCompletionSource<bool> StopLoop { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// 记录是否已释放的作用域资源。
    /// </summary>
    public sealed class ScopedResource : IDisposable
    {
        /// <summary>
        /// 获取资源是否已释放。
        /// </summary>
        public bool Disposed { get; private set; }
        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// 释放时抛出异常的作用域资源。
    /// </summary>
    public sealed class ThrowingScopedResource : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => throw new InvalidOperationException("scope disposal error");
    }

    /// <summary>
    /// 在同步初始化阶段触发失败并记录释放事件的模块。
    /// </summary>
    public class ScopeFailureModule : BingModule, IDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        private Recorder _recorder;

        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider)
        {
            _recorder = provider.GetRequiredService<Recorder>();
            provider.GetRequiredService<ThrowingScopedResource>();
            throw new InvalidOperationException("original startup error");
        }
        /// <inheritdoc />
        public void Dispose() => _recorder?.Events.Add("scope-module-disposed");
    }

    /// <summary>
    /// 在初始化阶段记录事件并抛出原始异常的日志失败测试模块。
    /// </summary>
    public class LoggingFailureModule : BingModule, IDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        private Recorder _recorder;

        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider)
        {
            _recorder = provider.GetRequiredService<Recorder>();
            _recorder.Events.Add("logging-start");
            throw new InvalidOperationException("original logging initialization failure");
        }

        /// <inheritdoc />
        public void Dispose() => _recorder?.Events.Add("logging-dispose");
    }

    /// <summary>
    /// 在 StartupLogger 解析日志记录器时抛出指定异常的测试工厂。
    /// </summary>
    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        /// <summary>
        /// 初始化测试日志工厂。
        /// </summary>
        /// <param name="error">需要由日志输出触发的异常。</param>
        public ThrowingLoggerFactory(Exception error) => Error = error;

        /// <summary>
        /// 获取待抛出的日志异常。
        /// </summary>
        private Exception Error { get; }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => throw Error;

        /// <inheritdoc />
        public void AddProvider(ILoggerProvider provider) { }

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 仅提供异步关闭和释放的测试模块。
    /// </summary>
    public class AsyncOnlyLifecycleModule : BingModule, IBingAsyncModuleShutdown, IAsyncDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        private Recorder _recorder;

        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) => _recorder = provider.GetRequiredService<Recorder>();

        /// <inheritdoc />
        public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            _recorder.Events.Add("async-only-stop");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _recorder?.Events.Add("async-only-dispose");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 记录子模块初始化、关闭和释放顺序的模块。
    /// </summary>
    public class ChildModule : BingModule, IBingModuleShutdown, IDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        protected Recorder Recorder;
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider)
        {
            Recorder = provider.GetRequiredService<Recorder>();
            Recorder.Events.Add("start-child");
        }
        /// <inheritdoc />
        public virtual void Shutdown(BingModuleShutdownContext context) => Recorder.Events.Add("stop-child");
        /// <inheritdoc />
        public virtual void Dispose() => Recorder?.Events.Add("dispose-child");
    }

    /// <summary>
    /// 记录父模块初始化、关闭和释放顺序的模块。
    /// </summary>
    [DependsOnModule(typeof(ChildModule))]
    public class ParentModule : BingModule, IBingModuleShutdown, IDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        protected Recorder Recorder;
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider)
        {
            Recorder = provider.GetRequiredService<Recorder>();
            Recorder.Events.Add("start-parent");
        }
        /// <inheritdoc />
        public virtual void Shutdown(BingModuleShutdownContext context) => Recorder.Events.Add("stop-parent");
        /// <inheritdoc />
        public void Dispose() => Recorder?.Events.Add("dispose-parent");
    }

    /// <summary>
    /// 初始化后抛出异常的模块。
    /// </summary>
    public class FailingModule : ParentModule
    {
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider)
        {
            base.UseModule(provider);
            throw new InvalidOperationException("initialization failure");
        }
    }

    /// <summary>
    /// 关闭钩子抛出异常的模块。
    /// </summary>
    public class FailingShutdownModule : ParentModule
    {
        /// <inheritdoc />
        public override void Shutdown(BingModuleShutdownContext context)
        {
            base.Shutdown(context);
            throw new InvalidOperationException("shutdown failure");
        }
    }

    /// <summary>
    /// 读取作用域资源的同步模块。
    /// </summary>
    public class ScopedModule : BingModule
    {
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) =>
            provider.GetRequiredService<Recorder>().Scoped = provider.GetRequiredService<ScopedResource>();
    }

    /// <summary>
    /// 使用异步初始化和关闭钩子的测试模块。
    /// </summary>
    public class AsyncModule : BingModule, IBingAsyncModuleInitializer, IBingAsyncModuleShutdown
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        private Recorder _recorder;
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) => throw new Exception("must not execute synchronous hook");
        /// <inheritdoc />
        public async Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
        {
            _recorder = context.ServiceProvider.GetRequiredService<Recorder>();
            _recorder.Scoped = context.ServiceProvider.GetRequiredService<ScopedResource>();
            _recorder.Events.Add("async-start");
            await _recorder.Gate.Task.WaitAsync(cancellationToken);
            _recorder.Events.Add("async-ready");
        }
        /// <inheritdoc />
        public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            _recorder.Events.Add("async-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 订阅事件并运行后台任务的异步资源模块。
    /// </summary>
    public class AsyncResourceModule : BingModule, IBingAsyncModuleInitializer, IBingAsyncModuleShutdown
    {
        /// <summary>
        /// 当前订阅的事件源。
        /// </summary>
        private TestEventSource _source;
        /// <summary>
        /// 当前资源状态。
        /// </summary>
        private AsyncResourceState _state;
        /// <summary>
        /// 控制后台任务停止的取消源。
        /// </summary>
        private CancellationTokenSource _stop;
        /// <summary>
        /// 当前后台任务。
        /// </summary>
        private Task _background;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddSingleton<TestEventSource>();
            services.AddSingleton<AsyncResourceState>();
            return services;
        }

        /// <inheritdoc />
        public async Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken)
        {
            _source = context.ServiceProvider.GetRequiredService<TestEventSource>();
            _state = context.ServiceProvider.GetRequiredService<AsyncResourceState>();
            _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _source.Published += OnPublished;
            _background = RunAsync(_stop.Token);
            await _state.Started.Task.WaitAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            if (_source == null) return;
            _source.Published -= OnPublished;
            _stop.Cancel();
            await _background.WaitAsync(cancellationToken);
            _state.ShutdownCount++;
            _stop.Dispose();
            _source = null;
        }

        /// <summary>
        /// 运行并等待后台资源任务。
        /// </summary>
        /// <param name="cancellationToken">后台任务取消令牌。</param>
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            _state.Started.TrySetResult(true);
            try
            {
                await _state.StopLoop.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                _state.Completed.TrySetResult(true);
            }
        }

        /// <summary>
        /// 处理事件源发布的事件。
        /// </summary>
        private void OnPublished() => _state.EventCallbackCount++;
    }

    /// <summary>
    /// 在异步初始化期间重入管理器的测试模块。
    /// </summary>
    public class ReentrantModule : BingModule, IBingAsyncModuleInitializer
    {
        /// <inheritdoc />
        public Task InitializeAsync(BingModuleInitializationContext context, CancellationToken cancellationToken) =>
            context.ServiceProvider.GetRequiredService<IBingModuleManager>().InitializeAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 同时验证异步关闭、异步释放和同步释放选择的模块。
    /// </summary>
    public class AsyncDisposableModule : BingModule, IBingAsyncModuleShutdown, IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// 当前模块使用的事件记录器。
        /// </summary>
        private Recorder _recorder;
        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) => _recorder = provider.GetRequiredService<Recorder>();
        /// <inheritdoc />
        public Task ShutdownAsync(BingModuleShutdownContext context, CancellationToken cancellationToken)
        {
            _recorder.Events.Add("async-only-stop");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _recorder?.Events.Add("async-dispose");
            return default;
        }
        /// <inheritdoc />
        public void Dispose() => _recorder?.Events.Add("sync-dispose");
    }
}
