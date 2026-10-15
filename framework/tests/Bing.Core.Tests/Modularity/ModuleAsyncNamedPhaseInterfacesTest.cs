using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证异步命名阶段接口及基类兼容行为。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleAsyncNamedPhaseInterfacesTest
{
    /// <summary>
    /// 基类默认接口不阻断旧入口。
    /// </summary>
    [Fact]
    public void DefaultModule_ImplementsAllPhaseInterfaces_AndLegacyRegistrationWorks()
    {
        var module = new DefaultModule();
        (module is IPreConfigureServices && module is IPostConfigureServices &&
         module is IOnPreApplicationInitialization && module is IOnPostApplicationInitialization &&
         module is IBingModuleShutdown)
            .ShouldBeTrue();

        var services = new ServiceCollection();
        services.AddBing().AddModule<DefaultModule>();
        using var provider = services.BuildServiceProvider();
        provider.UseBing();
        provider.ShutdownBing();
    }

    /// <summary>
    /// 五个统一接口分别声明同步与异步方法。
    /// </summary>
    [Fact]
    public void UnifiedInterfaces_ExposeBothMethods_AndRemovedTypesAreAbsent()
    {
        var contracts = new[]
        {
            (typeof(IPreConfigureServices), "PreConfigureServices", "PreConfigureServicesAsync"),
            (typeof(IPostConfigureServices), "PostConfigureServices", "PostConfigureServicesAsync"),
            (typeof(IOnPreApplicationInitialization), "OnPreApplicationInitialization",
                "OnPreApplicationInitializationAsync"),
            (typeof(IOnPostApplicationInitialization), "OnPostApplicationInitialization",
                "OnPostApplicationInitializationAsync"),
            (typeof(IBingModuleShutdown), "Shutdown", "ShutdownAsync")
        };
        foreach (var (contract, synchronous, asynchronous) in contracts)
        {
            contract.GetMethods().Select(method => method.Name)
                .ShouldBe(new[] { synchronous, asynchronous }, ignoreOrder: true);
            contract.GetMethod(asynchronous).ReturnType.ShouldBe(typeof(Task));
            contract.GetMethod(asynchronous).GetParameters().Last().ParameterType
                .ShouldBe(typeof(CancellationToken));
        }

        foreach (var name in new[]
                 {
                     "IAsyncPreConfigureServices", "IAsyncPostConfigureServices",
                     "IAsyncOnPreApplicationInitialization", "IAsyncOnPostApplicationInitialization",
                     "IBingAsyncModuleShutdown"
                 })
            typeof(BingModule).Assembly.GetType("Bing.Core.Modularity." + name).ShouldBeNull();
    }

    /// <summary>
    /// 异步接口按依赖顺序执行全部全局阶段。
    /// </summary>
    [Fact]
    public async Task AsyncInterfaces_RunGlobalPhasesOnceInDependencyOrder()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<SecondModule>(o => o.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[]
        {
            "config-pre-first", "config-pre-second", "config-post-first", "config-post-second",
            "init-pre-first", "init-pre-second", "init-first", "init-second",
            "init-post-first", "init-post-second"
        });
    }

    /// <summary>
    /// 同步重写优先于独立异步接口。
    /// </summary>
    [Fact]
    public void SyncOverrides_ShadowIndependentAsyncInterfaces()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<SyncOverrideModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        provider.UseBing();
        provider.ShutdownBing();

        events.ShouldBe(new[] { "sync-config", "sync-post-config", "sync-init", "sync-post-init" });
    }

    /// <summary>
    /// 异步重写优先于同步重写和独立异步接口。
    /// </summary>
    [Fact]
    public async Task AsyncOverrides_TakePriorityOverOtherImplementations()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<AsyncOverrideModule>(o => o.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[] { "override-config", "override-post-config", "override-init", "override-post-init" });
    }

    /// <summary>
    /// 中间基类的异步重写优先于派生类同名隐藏方法。
    /// </summary>
    [Fact]
    public async Task InheritedAsyncOverrides_IgnoreHiddenInterfaceMethods()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<InheritedOverrideModule>(o => o.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[] { "inherited-config", "inherited-init" });
    }

    /// <summary>
    /// 同名隐藏方法可隐式实现异步接口。
    /// </summary>
    [Fact]
    public async Task HiddenMethods_ImplementAsyncInterfaces()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<ImplicitAsyncModule>(o => o.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[] { "implicit-config", "implicit-init" });
    }

    /// <summary>
    /// 独立异步接口调用基类时不会再次分派接口。
    /// </summary>
    [Fact]
    public async Task AsyncInterfaceCallingBase_DoesNotRepeatOrFallThroughToSync()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<AsyncBridgeModule>(o => o.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();

        events.ShouldBe(new[] { "async-config", "async-post-config", "async-init", "async-post-init" });
    }

    /// <summary>
    /// 同步注册在调用任何配置阶段前拒绝独立异步接口。
    /// </summary>
    [Fact]
    public void SyncRegistration_RejectsAsyncInterfacesBeforeAnyHook()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);

        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<SecondModule>())
            .Message.ShouldContain("AddBingApplicationAsync");
        events.ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<DefaultModule>());
    }

    /// <summary>
    /// 同步初始化在调用任何初始化阶段前拒绝独立异步接口。
    /// </summary>
    [Fact]
    public void SyncInitialization_RejectsAsyncInterfacesBeforeAnyHook()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<AsyncInitializationModule>(o => o.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.UseBing())
            .Message.ShouldContain("UseBingAsync");
        events.ShouldBeEmpty();
    }

    /// <summary>
    /// 同步入口在任何回调前拒绝仅使用后置异步接口的模块。
    /// </summary>
    [Fact]
    public void SyncEntries_RejectPostOnlyAsyncInterfacesBeforeAnyHook()
    {
        var events = new List<string>();
        var configuration = new ServiceCollection().AddSingleton(events);
        Should.Throw<InvalidOperationException>(() =>
            configuration.AddBingApplication<PostOnlyAsyncConfigurationModule>())
            .Message.ShouldContain("AddBingApplicationAsync");
        events.ShouldBeEmpty();

        var initialization = new ServiceCollection().AddSingleton(events);
        initialization.AddBingApplication<PostOnlyAsyncInitializationModule>(o => o.AutoRegisterServices = false);
        using var provider = initialization.BuildServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.UseBing())
            .Message.ShouldContain("UseBingAsync");
        events.ShouldBeEmpty();
    }

    /// <summary>
    /// 旧入口拒绝实际使用的异步服务配置接口。
    /// </summary>
    [Fact]
    public void LegacyRegistration_RejectsIndependentAsyncConfiguration()
    {
        var builder = new ServiceCollection().AddBing();
        Should.Throw<InvalidOperationException>(() => builder.AddModule<FirstModule>())
            .Message.ShouldContain("AddBingApplication");
    }

    /// <summary>
    /// 异步阶段失败保留原始异常并清理模块。
    /// </summary>
    [Theory]
    [InlineData("config-pre")]
    [InlineData("config-post")]
    [InlineData("init-pre")]
    [InlineData("init-post")]
    public async Task AsyncInterfaceFailure_StopsFollowingStagesAndReleasesResources(string phase)
    {
        var state = new FailureState(phase);
        var services = new ServiceCollection().AddSingleton(state);
        if (phase.StartsWith("config-", StringComparison.Ordinal))
        {
            var error = await Should.ThrowAsync<InvalidOperationException>(() =>
                services.AddBingApplicationAsync<FailureModule>(o => o.AutoRegisterServices = false));
            error.Message.ShouldBe("failure-" + phase);
            Should.Throw<InvalidOperationException>(() => services.AddBingApplication<DefaultModule>());
            state.Events.ShouldNotContain("init-main");
            state.Events.ShouldNotContain("shutdown");
        }
        else
        {
            await services.AddBingApplicationAsync<FailureModule>(o => o.AutoRegisterServices = false);
            await using var provider = services.BuildServiceProvider();
            var error = await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());
            error.Message.ShouldBe("failure-" + phase);
            error.Data["Bing.ModulePhase"].ShouldBe(phase == "init-pre" ? "PreInitialize" : "PostInitialize");
            state.Events.Count(x => x == "shutdown").ShouldBe(1);
            if (phase == "init-pre") state.Events.ShouldNotContain("init-main");
        }
        state.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 异步阶段取消后清理模块并停止后续回调。
    /// </summary>
    [Theory]
    [InlineData("config-pre")]
    [InlineData("config-post")]
    [InlineData("init-pre")]
    [InlineData("init-post")]
    public async Task AsyncInterfaceCancellation_ReleasesResources(string phase)
    {
        var state = new FailureState(phase) { Cancel = true };
        var services = new ServiceCollection().AddSingleton(state);
        if (phase.StartsWith("config-", StringComparison.Ordinal))
            await Should.ThrowAsync<OperationCanceledException>(() =>
                services.AddBingApplicationAsync<FailureModule>(o => o.AutoRegisterServices = false));
        else
        {
            await services.AddBingApplicationAsync<FailureModule>(o => o.AutoRegisterServices = false);
            await using var provider = services.BuildServiceProvider();
            await Should.ThrowAsync<OperationCanceledException>(() => provider.UseBingAsync());
            state.Events.Count(x => x == "shutdown").ShouldBe(1);
        }
        state.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 默认阶段模块。
    /// </summary>
    public class DefaultModule : BingModule { }

    /// <summary>
    /// 按阶段记录异步接口调用。
    /// </summary>
    public abstract class OrderedModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <summary>
        /// 获取模块名称。
        /// </summary>
        protected abstract string Name { get; }

        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "config-pre-" + Name);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "config-post-" + Name);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-pre-" + Name);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-post-" + Name);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("init-" + Name);
    }

    /// <summary>
    /// 首个模块。
    /// </summary>
    public class FirstModule : OrderedModule
    {
        /// <inheritdoc />
        protected override string Name => "first";

        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Business;
    }

    /// <summary>
    /// 依赖首个模块的第二个模块。
    /// </summary>
    [DependsOnModule(typeof(FirstModule))]
    public class SecondModule : OrderedModule
    {
        /// <inheritdoc />
        protected override string Name => "second";

        /// <inheritdoc />
        public override ModuleLevel Level => ModuleLevel.Core;
    }

    /// <summary>
    /// 用同步重写覆盖异步接口的模块。
    /// </summary>
    public class SyncOverrideModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) => Record(services, "sync-config");

        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) => Record(services, "sync-post-config");

        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-init");

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-post-init");

        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            throw new InvalidOperationException("shadowed config interface");

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            throw new InvalidOperationException("shadowed post config interface");

        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            throw new InvalidOperationException("shadowed init interface");

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            throw new InvalidOperationException("shadowed post init interface");
    }

    /// <summary>
    /// 用异步重写覆盖其他回调的模块。
    /// </summary>
    public class AsyncOverrideModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) =>
            throw new InvalidOperationException("shadowed sync config");

        /// <inheritdoc />
        public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "override-config");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) =>
            throw new InvalidOperationException("shadowed sync post config");

        /// <inheritdoc />
        public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "override-post-config");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            throw new InvalidOperationException("shadowed sync init");

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("override-init");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            throw new InvalidOperationException("shadowed sync post init");

        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("override-post-init");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            throw new InvalidOperationException("shadowed async config interface");

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            throw new InvalidOperationException("shadowed async post config interface");

        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            throw new InvalidOperationException("shadowed async init interface");

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            throw new InvalidOperationException("shadowed async post init interface");
    }

    /// <summary>
    /// 在中间基类提供异步重写的模块。
    /// </summary>
    public abstract class IntermediateOverrideModule : BingModule
    {
        /// <inheritdoc />
        public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "inherited-config");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("inherited-init");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 通过同名方法隐藏继承重写的模块。
    /// </summary>
    public class InheritedOverrideModule : IntermediateOverrideModule, IPreConfigureServices,
        IOnPreApplicationInitialization
    {
        /// <inheritdoc />
        public new Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            throw new InvalidOperationException("hidden config must not run");

        /// <inheritdoc />
        public new Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken token) => throw new InvalidOperationException("hidden init must not run");
    }

    /// <summary>
    /// 用同名隐藏方法实现异步接口的模块。
    /// </summary>
    public class ImplicitAsyncModule : BingModule, IPreConfigureServices, IOnPreApplicationInitialization
    {
        /// <inheritdoc />
        public new Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken token)
        {
            Record(services, "implicit-config");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public new Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("implicit-init");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 验证异步接口调用基类的模块。
    /// </summary>
    public class AsyncBridgeModule : BingModule, IPreConfigureServices,
        IPostConfigureServices, IOnPreApplicationInitialization,
        IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        async Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services,
            CancellationToken token)
        {
            Record(services, "async-config");
            await base.PreConfigureServicesAsync(services, token);
        }

        /// <inheritdoc />
        void IPreConfigureServices.PreConfigureServices(IServiceCollection services) =>
            throw new InvalidOperationException("sync config must not run");

        /// <inheritdoc />
        async Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services,
            CancellationToken token)
        {
            Record(services, "async-post-config");
            await base.PostConfigureServicesAsync(services, token);
        }

        /// <inheritdoc />
        void IPostConfigureServices.PostConfigureServices(IServiceCollection services) =>
            throw new InvalidOperationException("sync post config must not run");

        /// <inheritdoc />
        async Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-init");
            await base.OnPreApplicationInitializationAsync(context, token);
        }

        /// <inheritdoc />
        void IOnPreApplicationInitialization.OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            throw new InvalidOperationException("sync init must not run");

        /// <inheritdoc />
        async Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-post-init");
            await base.OnPostApplicationInitializationAsync(context, token);
        }

        /// <inheritdoc />
        void IOnPostApplicationInitialization.OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            throw new InvalidOperationException("sync post init must not run");
    }

    /// <summary>
    /// 仅后置配置阶段使用独立异步接口的模块。
    /// </summary>
    public class PostOnlyAsyncConfigurationModule : BingModule, IPostConfigureServices
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) => Record(services, "unexpected");

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// 仅后置初始化阶段使用独立异步接口的模块。
    /// </summary>
    public class PostOnlyAsyncInitializationModule : BingModule, IOnPostApplicationInitialization
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("unexpected");

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) => Task.CompletedTask;
    }

    /// <summary>
    /// 只有初始化阶段使用异步接口的模块。
    /// </summary>
    public class AsyncInitializationModule : BingModule, IOnPreApplicationInitialization
    {
        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("unexpected");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 保存故障阶段和资源释放次数。
    /// </summary>
    public class FailureState
    {
        /// <summary>
        /// 初始化一个 <see cref="FailureState" /> 类型的实例。
        /// </summary>
        /// <param name="phase">故障阶段。</param>
        public FailureState(string phase) => Phase = phase;

        /// <summary>
        /// 获取故障阶段。
        /// </summary>
        public string Phase { get; }

        /// <summary>
        /// 获取或设置是否取消。
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// 获取阶段事件。
        /// </summary>
        public List<string> Events { get; } = new();

        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int Disposals { get; set; }
    }

    /// <summary>
    /// 在指定异步阶段失败的模块。
    /// </summary>
    public class FailureModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization,
        IBingModuleShutdown, IAsyncDisposable
    {
        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            Execute(GetState(services), "config-pre", token);

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services, CancellationToken token) =>
            Execute(GetState(services), "config-post", token);

        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            Execute(context.ServiceProvider.GetRequiredService<FailureState>(), "init-pre", token);

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken token) =>
            Execute(context.ServiceProvider.GetRequiredService<FailureState>(), "init-post", token);

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<FailureState>().Events.Add("init-main");

        /// <inheritdoc />
        Task IBingModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<FailureState>().Events.Add("shutdown");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            State.Disposals++;
            return default;
        }

        /// <summary>
        /// 获取本次注册的故障状态。
        /// </summary>
        private FailureState State { get; set; }

        /// <summary>
        /// 执行故障阶段。
        /// </summary>
        private Task Execute(FailureState state, string phase, CancellationToken token)
        {
            State = state;
            state.Events.Add(phase);
            if (state.Phase == phase)
            {
                if (state.Cancel) throw new OperationCanceledException(token);
                throw new InvalidOperationException("failure-" + phase);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取服务集合中的故障状态。
        /// </summary>
        private static FailureState GetState(IServiceCollection services) =>
            (FailureState)services.Last(x => x.ServiceType == typeof(FailureState)).ImplementationInstance;
    }

    /// <summary>
    /// 记录服务配置事件。
    /// </summary>
    private static void Record(IServiceCollection services, string value) =>
        ((List<string>)services.Last(x => x.ServiceType == typeof(List<string>)).ImplementationInstance).Add(value);
}
