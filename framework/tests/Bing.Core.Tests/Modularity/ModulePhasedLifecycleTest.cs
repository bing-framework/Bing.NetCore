using Bing.Core.Modularity;
using Bing.Core.Tests.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证模块分阶段生命周期和异步注册契约。
/// </summary>
[Collection("Module static compatibility")]
public class ModulePhasedLifecycleTest
{
    /// <summary>
    /// 依赖顺序优先于模块级别，且每个初始化阶段遍历全部模块。
    /// </summary>
    [Fact]
    public void Initialization_RunsGlobalPhasesInDependencyOrder()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<LaterModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[]
        {
            "pre-first", "pre-later", "main-first", "main-later",
            "post-first", "post-later", "stop-later", "stop-first"
        });
    }

    /// <summary>
    /// 同步注册在任何配置钩子前拒绝异步配置，异步注册只执行异步重写。
    /// </summary>
    [Fact]
    public async Task AsyncServiceHook_RequiresAsyncRegistration_AndRunsOnce()
    {
        var events = new List<string>();
        var rejected = new ServiceCollection().AddSingleton(events);
        Should.Throw<InvalidOperationException>(() => rejected.AddBingApplication<AsyncConfigurationModule>())
            .Message.ShouldContain("AddBingApplicationAsync");
        events.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => rejected.AddBingApplication<FirstModule>());

        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<AsyncConfigurationModule>(o => o.AutoRegisterServices = false);
        events.ShouldBe(new[] { "async-config" });
        await using var provider = services.BuildServiceProvider();
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        events.ShouldBe(new[] { "async-config", "async-pre", "async-main", "async-post", "async-stop" });
    }

    /// <summary>
    /// 预初始化失败时关闭已开始的模块，保留原始异常和阶段。
    /// </summary>
    [Fact]
    public void PreInitializationFailure_StopsStartedModuleOnly()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<PreFailModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<InvalidOperationException>(() => provider.UseBing());
        error.Message.ShouldBe("pre-failure");
        error.Data["Bing.ModulePhase"].ShouldBe("PreInitialize");
        events.ShouldBe(new[] { "pre-fail", "stop-fail" });
    }

    /// <summary>
    /// 后初始化失败时全部已启动模块逆序关闭，后续模块不再执行。
    /// </summary>
    [Fact]
    public void PostInitializationFailure_StopsAllStartedModulesInReverseOrder()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<PostFailModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        var error = Should.Throw<InvalidOperationException>(() => provider.UseBing());
        error.Message.ShouldBe("post-failure");
        error.Data["Bing.ModulePhase"].ShouldBe("PostInitialize");
        events.ShouldBe(new[]
        {
            "pre-first", "pre-later", "main-first", "main-later", "post-first",
            "post-fail", "stop-fail", "stop-first"
        });
    }

    /// <summary>
    /// 两种扫描模式按指定时机执行，且后配置可替换已扫描服务。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible, false)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices, true)]
    public void ServiceRegistrationMode_ControlsScanTiming(BingServiceRegistrationMode mode, bool visibleInMain)
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        var eventCount = 0;
        Action<Type> handler = type =>
        {
            if (type == typeof(ScanTimingModule)) eventCount++;
        };
        BingLoader.RegisterType += handler;
        try
        {
            services.AddBingApplication<ScanTimingModule>(options => options.ServiceRegistrationMode = mode);
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ModuleRegistrationTest.IAutoRegisterProbe>()
                .ShouldBeOfType<ReplacementProbe>();
            events.ShouldBe(new[] { "scan-visible:" + visibleInMain });
            eventCount.ShouldBe(1);
        }
        finally { BingLoader.RegisterType -= handler; }
    }

    /// <summary>
    /// 关闭自动 DI 后提前模式仍执行公开类型事件。
    /// </summary>
    [Fact]
    public void EarlyScan_WithAutoRegistrationDisabled_StillRaisesTypeEvent()
    {
        var services = new ServiceCollection();
        var eventCount = 0;
        Action<Type> handler = type =>
        {
            if (type == typeof(FirstModule)) eventCount++;
        };
        BingLoader.RegisterType += handler;
        try
        {
            services.AddBingApplication<FirstModule>(options =>
            {
                options.AutoRegisterServices = false;
                options.ServiceRegistrationMode = BingServiceRegistrationMode.BeforeConfigureServices;
            });
            eventCount.ShouldBe(1);
            using var provider = services.BuildServiceProvider();
            provider.GetService<ModuleRegistrationTest.IAutoRegisterProbe>().ShouldBeNull();
        }
        finally { BingLoader.RegisterType -= handler; }
    }

    /// <summary>
    /// 异步配置进行时拒绝重复注册和提前构建。
    /// </summary>
    [Fact]
    public async Task AsyncRegistration_InProgress_RejectsReentryAndEarlyBuild()
    {
        var gate = new ConfigurationGate();
        var services = new ServiceCollection().AddSingleton(gate);
        var pending = services.AddBingApplicationAsync<BlockedConfigurationModule>(
            options => options.AutoRegisterServices = false);
        await gate.Entered.Task;

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<FirstModule>());
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
        gate.Release.TrySetResult(true);
        await pending;

        await using var provider = services.BuildBingServiceProvider();
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        gate.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 异步配置取消后标记失败，等待模块资源释放并禁止重试。
    /// </summary>
    [Fact]
    public async Task AsyncRegistration_Cancellation_ReleasesModuleAndPreventsRetry()
    {
        var gate = new ConfigurationGate();
        var services = new ServiceCollection().AddSingleton(gate);
        using var cancellation = new CancellationTokenSource();
        var pending = services.AddBingApplicationAsync<BlockedConfigurationModule>(
            options => options.AutoRegisterServices = false, cancellation.Token);
        await gate.Entered.Task;

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        gate.Disposals.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<FirstModule>());
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
    }

    /// <summary>
    /// 三个初始化阶段共享短期作用域，并在初始化返回前释放。
    /// </summary>
    [Fact]
    public void InitializationPhases_ShareScopeAndDisposeItBeforeReturning()
    {
        var observed = new ScopeObservation();
        var services = new ServiceCollection().AddSingleton(observed).AddScoped<PhaseScopedService>();
        services.AddBingApplication<ScopeModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        provider.UseBing();

        observed.Instances.Count.ShouldBe(3);
        observed.Instances.All(instance => ReferenceEquals(instance, observed.Instances[0])).ShouldBeTrue();
        observed.Instances[0].Disposed.ShouldBeTrue();
        provider.ShutdownBing();
    }

    /// <summary>
    /// 同名隐藏方法不能抢占基类虚钩子分派。
    /// </summary>
    [Fact]
    public void HiddenMethod_DoesNotReplaceLegacyInitialization()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<HiddenHookModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();

        events.ShouldBe(new[] { "legacy-use" });
        provider.ShutdownBing();
    }

    /// <summary>
    /// 旧入口遇到新服务配置钩子时提示迁移并禁止继续注册。
    /// </summary>
    [Fact]
    public void LegacyEntry_RejectsNewServiceHooksWithMigrationMessage()
    {
        var services = new ServiceCollection();
        var builder = services.AddBing();

        var error = Should.Throw<InvalidOperationException>(() => builder.AddModule<ScanTimingModule>());

        error.Message.ShouldContain("AddBingApplication");
        Should.Throw<InvalidOperationException>(() => builder.AddModule<FirstModule>());
    }

    /// <summary>
    /// 任一配置阶段失败均终止后续阶段并只释放一次模块。
    /// </summary>
    [Theory]
    [InlineData("PreConfigureServices", "PreConfigureServices")]
    [InlineData("ConfigureServices", "AddServices")]
    [InlineData("PostConfigureServices", "PostConfigureServices")]
    public void ServiceConfigurationFailure_StopsLaterStagesAndReleasesModule(
        string failingStage, string diagnosticPhase)
    {
        var state = new ConfigurationStageState { FailingStage = failingStage };
        var services = new ServiceCollection().AddSingleton(state);

        var error = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<ConfigurationStageModule>(options => options.AutoRegisterServices = false));

        error.Message.ShouldBe("configuration failure");
        error.Data["Bing.ModulePhase"].ShouldBe(diagnosticPhase);
        state.Stages.ShouldBe(failingStage switch
        {
            "PreConfigureServices" => new[] { "PreConfigureServices" },
            "ConfigureServices" => new[] { "PreConfigureServices", "ConfigureServices" },
            _ => new[] { "PreConfigureServices", "ConfigureServices", "PostConfigureServices" }
        });
        state.Disposals.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<FirstModule>());
    }

    /// <summary>
    /// 任一异步配置阶段取消均等待释放资源并阻止后续阶段。
    /// </summary>
    [Theory]
    [InlineData("PreConfigureServices")]
    [InlineData("ConfigureServices")]
    [InlineData("PostConfigureServices")]
    public async Task AsyncServiceConfigurationCancellation_ReleasesModule(string canceledStage)
    {
        var state = new ConfigurationStageState { FailingStage = canceledStage };
        var services = new ServiceCollection().AddSingleton(state);
        using var cancellation = new CancellationTokenSource();
        var pending = services.AddBingApplicationAsync<CancelableConfigurationStageModule>(
            options => options.AutoRegisterServices = false, cancellation.Token);
        await state.Entered.Task;

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        state.Stages.Last().ShouldBe(canceledStage);
        state.Stages.Count.ShouldBe(Array.IndexOf(new[]
            { "PreConfigureServices", "ConfigureServices", "PostConfigureServices" }, canceledStage) + 1);
        state.Disposals.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
    }

    /// <summary>
    /// 任一初始化阶段取消时终止后续阶段并逆序关闭。
    /// </summary>
    [Theory]
    [InlineData("PreInitialize")]
    [InlineData("Initialize")]
    [InlineData("PostInitialize")]
    public async Task InitializationPhaseCancellation_StopsAndReleasesModule(string canceledStage)
    {
        var state = new InitializationStageState { CanceledStage = canceledStage };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<CancelableInitializationModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var pending = provider.UseBingAsync(cancellation.Token);
        await state.Entered.Task;

        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => pending);

        state.Stages.Last().ShouldBe(canceledStage);
        state.Stages.Count.ShouldBe(Array.IndexOf(new[]
            { "PreInitialize", "Initialize", "PostInitialize" }, canceledStage) + 1);
        state.Shutdowns.ShouldBe(1);
        state.Disposals.ShouldBe(1);
        await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());
    }

    /// <summary>
    /// 为测试模块提供继承的三个阶段实现。
    /// </summary>
    public abstract class PhaseBase : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("pre-" + Name);

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("main-" + Name);

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("post-" + Name);

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("stop-" + Name);

        /// <summary>
        /// 事件标识。
        /// </summary>
        protected abstract string Name { get; }
    }

    /// <summary>
    /// 依赖模块。
    /// </summary>
    public class FirstModule : PhaseBase
    {
        /// <inheritdoc />
        protected override string Name => "first";
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Business;
    }

    /// <summary>
    /// 依赖方模块。
    /// </summary>
    [DependsOnModule(typeof(FirstModule))]
    public class LaterModule : PhaseBase
    {
        /// <inheritdoc />
        protected override string Name => "later";
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;
    }

    /// <summary>
    /// 异步钩子模块。
    /// </summary>
    public class AsyncConfigurationModule : BingModule
    {
        /// <inheritdoc />
        public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance
                .ShouldBeOfType<List<string>>().Add("async-config");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-pre");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-main");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-post");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 预初始化失败模块。
    /// </summary>
    public class PreFailModule : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("pre-fail");
            throw new InvalidOperationException("pre-failure");
        }

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("stop-fail");
    }

    /// <summary>
    /// 后初始化失败模块。
    /// </summary>
    [DependsOnModule(typeof(FirstModule))]
    public class PostFailModule : PhaseBase
    {
        /// <inheritdoc />
        protected override string Name => "later";

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("post-fail");
            throw new InvalidOperationException("post-failure");
        }

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("stop-fail");
    }

    /// <summary>
    /// 扫描时机探针模块。
    /// </summary>
    public class ScanTimingModule : BingModule
    {
        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;

        /// <inheritdoc />
        public override void ConfigureServices(IServiceCollection services)
        {
            var visible = services.Any(descriptor =>
                descriptor.ServiceType == typeof(ModuleRegistrationTest.IAutoRegisterProbe));
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance)
                .Add("scan-visible:" + visible);
        }

        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) =>
            services.Replace(ServiceDescriptor.Singleton<ModuleRegistrationTest.IAutoRegisterProbe, ReplacementProbe>());
    }

    /// <summary>
    /// 用于验证后配置替换的服务。
    /// </summary>
    public class ReplacementProbe : ModuleRegistrationTest.IAutoRegisterProbe { }

    /// <summary>
    /// 控制异步配置暂停并记录资源释放。
    /// </summary>
    public sealed class ConfigurationGate
    {
        /// <summary>
        /// 配置阶段已开始。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 允许配置阶段继续。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 模块资源释放次数。
        /// </summary>
        public int Disposals { get; set; }
    }

    /// <summary>
    /// 可暂停的异步配置模块。
    /// </summary>
    public class BlockedConfigurationModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 当前注册的测试门闩。
        /// </summary>
        private ConfigurationGate _gate;

        /// <inheritdoc />
        public override async Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            _gate = (ConfigurationGate)services.First(x => x.ServiceType == typeof(ConfigurationGate)).ImplementationInstance;
            _gate.Entered.TrySetResult(true);
            await _gate.Release.Task.WaitAsync(cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _gate.Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 记录初始化阶段解析到的作用域服务。
    /// </summary>
    public sealed class ScopeObservation
    {
        /// <summary>
        /// 各阶段解析到的实例。
        /// </summary>
        public List<PhaseScopedService> Instances { get; } = new();
    }

    /// <summary>
    /// 作用域释放探针。
    /// </summary>
    public sealed class PhaseScopedService : IDisposable
    {
        /// <summary>
        /// 是否已经释放。
        /// </summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// 跨阶段读取作用域服务的模块。
    /// </summary>
    public class ScopeModule : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) => Observe(context);
        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) => Observe(context);
        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) => Observe(context);

        /// <summary>
        /// 记录当前阶段的作用域服务。
        /// </summary>
        private static void Observe(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<ScopeObservation>().Instances.Add(
                context.ServiceProvider.GetRequiredService<PhaseScopedService>());
    }

    /// <summary>
    /// 包含同名隐藏方法的旧模块。
    /// </summary>
    public class HiddenHookModule : BingModule
    {
        /// <summary>
        /// 同名方法不应视为虚钩子的重写。
        /// </summary>
        public new void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("hidden");

        /// <inheritdoc />
        public override void UseModule(IServiceProvider provider) =>
            provider.GetRequiredService<List<string>>().Add("legacy-use");
    }

    /// <summary>
    /// 保存阶段故障选择和释放次数。
    /// </summary>
    public sealed class ConfigurationStageState
    {
        /// <summary>
        /// 指定失败或取消的阶段。
        /// </summary>
        public string FailingStage { get; set; }
        /// <summary>
        /// 已进入的配置阶段。
        /// </summary>
        public List<string> Stages { get; } = new();
        /// <summary>
        /// 异步阶段已进入。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 模块释放次数。
        /// </summary>
        public int Disposals { get; set; }
    }

    /// <summary>
    /// 同步配置阶段故障模块。
    /// </summary>
    public class ConfigurationStageModule : BingModule, IDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private ConfigurationStageState _state;

        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) => Visit(services, "PreConfigureServices");
        /// <inheritdoc />
        public override void ConfigureServices(IServiceCollection services) => Visit(services, "ConfigureServices");
        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) => Visit(services, "PostConfigureServices");

        /// <summary>
        /// 进入并检查指定配置阶段。
        /// </summary>
        private void Visit(IServiceCollection services, string phase)
        {
            _state = (ConfigurationStageState)services.First(x => x.ServiceType == typeof(ConfigurationStageState))
                .ImplementationInstance;
            _state.Stages.Add(phase);
            if (_state.FailingStage == phase) throw new InvalidOperationException("configuration failure");
        }

        /// <inheritdoc />
        public void Dispose() => _state.Disposals++;
    }

    /// <summary>
    /// 异步配置阶段取消模块。
    /// </summary>
    public class CancelableConfigurationStageModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private ConfigurationStageState _state;

        /// <inheritdoc />
        public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            VisitAsync(services, "PreConfigureServices", cancellationToken);
        /// <inheritdoc />
        public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            VisitAsync(services, "ConfigureServices", cancellationToken);
        /// <inheritdoc />
        public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            VisitAsync(services, "PostConfigureServices", cancellationToken);

        /// <summary>
        /// 进入指定阶段并等待测试取消。
        /// </summary>
        private async Task VisitAsync(IServiceCollection services, string phase, CancellationToken token)
        {
            _state = (ConfigurationStageState)services.First(x => x.ServiceType == typeof(ConfigurationStageState))
                .ImplementationInstance;
            _state.Stages.Add(phase);
            if (_state.FailingStage != phase) return;
            _state.Entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 保存初始化阶段取消与资源清理结果。
    /// </summary>
    public sealed class InitializationStageState
    {
        /// <summary>
        /// 指定取消阶段。
        /// </summary>
        public string CanceledStage { get; set; }
        /// <summary>
        /// 已进入的阶段。
        /// </summary>
        public List<string> Stages { get; } = new();
        /// <summary>
        /// 取消阶段已进入。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 关闭次数。
        /// </summary>
        public int Shutdowns { get; set; }
        /// <summary>
        /// 模块释放次数。
        /// </summary>
        public int Disposals { get; set; }
    }

    /// <summary>
    /// 用于逐阶段取消初始化的模块。
    /// </summary>
    public class CancelableInitializationModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private InitializationStageState _state;

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "PreInitialize", cancellationToken);
        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "Initialize", cancellationToken);
        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "PostInitialize", cancellationToken);

        /// <summary>
        /// 进入指定初始化阶段并等待取消。
        /// </summary>
        private async Task VisitAsync(BingModuleInitializationContext context, string phase, CancellationToken token)
        {
            _state = context.ServiceProvider.GetRequiredService<InitializationStageState>();
            _state.Stages.Add(phase);
            if (_state.CanceledStage != phase) return;
            _state.Entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        }

        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<InitializationStageState>().Shutdowns++;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
