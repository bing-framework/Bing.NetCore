using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证模块分阶段生命周期的兼容与清理边界。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleLifecycleReviewFixTest
{
    /// <summary>
    /// 混合前置钩子时依旧按模块依赖的逆序关闭。
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MixedPreHooks_ShutdownInReverseModuleOrder(bool asynchronous, bool failPost)
    {
        var state = new ReviewState { FailPost = failPost };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<ConsumerModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        if (asynchronous)
        {
            if (failPost)
                await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());
            else
            {
                await provider.UseBingAsync();
                await provider.ShutdownBingAsync();
            }
        }
        else if (failPost)
            Should.Throw<InvalidOperationException>(() => provider.UseBing());
        else
        {
            provider.UseBing();
            provider.ShutdownBing();
        }

        state.Events.ShouldBe(new[]
        {
            "pre-consumer", "init-dependency", "init-consumer",
            "stop-consumer", "stop-dependency"
        });
        state.ConsumerShutdowns.ShouldBe(1);
        state.DependencyShutdowns.ShouldBe(1);
    }

    /// <summary>
    /// 中间基类的异步配置重写不能被派生类同名隐藏方法遮蔽。
    /// </summary>
    [Fact]
    public async Task HiddenAsyncConfiguration_StillRequiresAsyncRegistration()
    {
        var state = new ReviewState();
        var rejected = new ServiceCollection().AddSingleton(state);
        Should.Throw<InvalidOperationException>(() => rejected.AddBingApplication<HiddenConfigurationModule>())
            .Message.ShouldContain("AddBingApplicationAsync");
        state.Events.ShouldBeEmpty();

        var services = new ServiceCollection().AddSingleton(state);
        await services.AddBingApplicationAsync<HiddenConfigurationModule>(o => o.AutoRegisterServices = false);
        state.Events.ShouldBe(new[] { "ancestor-config" });
    }

    /// <summary>
    /// 同步初始化在任何前置副作用之前发现继承的异步主钩子。
    /// </summary>
    [Fact]
    public async Task HiddenAsyncInitialization_RejectsSyncBeforeAnyPreHook()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<HiddenInitializationModule>(options =>
        {
            options.AutoRegisterServices = false;
            options.AdditionalModules.Add(typeof(PreSideEffectModule));
        });
        await using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.UseBing()).Message.ShouldContain("UseBingAsync");
        state.Events.ShouldBeEmpty();
        await provider.UseBingAsync();
        state.Events.ShouldContain("ancestor-init");
        state.Events.ShouldNotContain("hidden-init");
    }

    /// <summary>
    /// 前置与后置阶段均能识别被隐藏的继承异步重写。
    /// </summary>
    [Fact]
    public async Task HiddenAsyncPreAndPostHooks_ExecuteInheritedVirtualSlots()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<HiddenPrePostModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();

        Should.Throw<InvalidOperationException>(() => provider.UseBing()).Message.ShouldContain("UseBingAsync");
        state.Events.ShouldBeEmpty();
        await provider.UseBingAsync();
        state.Events.ShouldBe(new[] { "ancestor-pre", "ancestor-post" });
    }

    /// <summary>
    /// 中间基类异步关闭重写被隐藏时仍需异步关闭并执行原虚方法。
    /// </summary>
    [Fact]
    public async Task HiddenAsyncShutdown_StillRequiresAsyncShutdown()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<HiddenShutdownModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();
        await provider.UseBingAsync();

        Should.Throw<InvalidOperationException>(() => provider.ShutdownBing());
        await provider.ShutdownBingAsync();
        state.Events.ShouldBe(new[] { "ancestor-stop" });
    }

    /// <summary>
    /// 最后的异步配置回调主动取消后，注册失败并等待释放。
    /// </summary>
    [Theory]
    [InlineData("PreConfigureServices", BingServiceRegistrationMode.BeforeConfigureServices)]
    [InlineData("ConfigureServices", BingServiceRegistrationMode.Compatible)]
    [InlineData("PostConfigureServices", BingServiceRegistrationMode.Compatible)]
    public async Task ConfigurationCallbackCancelsAndReturns_RegistrationFails(
        string cancelPhase, BingServiceRegistrationMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ReviewState { Cancellation = cancellation, CancelPhase = cancelPhase };
        var services = new ServiceCollection().AddSingleton(state);
        var scanCalls = 0;
        Action<Type> scan = type =>
        {
            if (type == typeof(CancelingConfigurationModule)) scanCalls++;
        };
        BingLoader.RegisterType += scan;
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() =>
                services.AddBingApplicationAsync<CancelingConfigurationModule>(options =>
                {
                    options.AutoRegisterServices = false;
                    options.ServiceRegistrationMode = mode;
                }, cancellation.Token));
        }
        finally { BingLoader.RegisterType -= scan; }

        state.Events.ShouldBe(cancelPhase == "PreConfigureServices"
            ? new[] { "PreConfigureServices" }
            : cancelPhase == "ConfigureServices"
                ? new[] { "PreConfigureServices", "ConfigureServices" }
                : new[] { "PreConfigureServices", "ConfigureServices", "PostConfigureServices" });
        if (cancelPhase == "PreConfigureServices") scanCalls.ShouldBe(0);
        state.Disposals.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
        Should.Throw<InvalidOperationException>(() => services.AddBingApplication<ConsumerModule>());
    }

    /// <summary>
    /// 末尾公开类型事件取消后也不得提交注册。
    /// </summary>
    [Theory]
    [InlineData(BingServiceRegistrationMode.Compatible)]
    [InlineData(BingServiceRegistrationMode.BeforeConfigureServices)]
    public async Task RegistrationEventCancels_RegistrationFails(BingServiceRegistrationMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        Action<Type> scan = type =>
        {
            if (type == typeof(CancelingConfigurationModule)) cancellation.Cancel();
        };
        BingLoader.RegisterType += scan;
        try
        {
            await Should.ThrowAsync<OperationCanceledException>(() =>
                services.AddBingApplicationAsync<CancelingConfigurationModule>(
                    options =>
                    {
                        options.AutoRegisterServices = false;
                        options.ServiceRegistrationMode = mode;
                    }, cancellation.Token));
        }
        finally { BingLoader.RegisterType -= scan; }
        state.Disposals.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => services.BuildBingServiceProvider());
    }

    /// <summary>
    /// 配置取消时注册任务等待模块异步释放完成。
    /// </summary>
    [Fact]
    public async Task CanceledRegistration_AwaitsAsyncModuleDisposal()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ReviewState
        {
            Cancellation = cancellation,
            CancelPhase = "PostConfigureServices",
            DisposeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            DisposeRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var services = new ServiceCollection().AddSingleton(state);
        var registration = services.AddBingApplicationAsync<CancelingConfigurationModule>(
            options => options.AutoRegisterServices = false, cancellation.Token);

        await state.DisposeEntered.Task;
        registration.IsCompleted.ShouldBeFalse();
        state.DisposeRelease.TrySetResult(true);
        await Should.ThrowAsync<OperationCanceledException>(() => registration);
        state.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 显式调用基类时续接旧接口，且每条回调只执行一次。
    /// </summary>
    [Fact]
    public async Task ExplicitBaseCalls_BridgeOldInterfacesOnce()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        await services.AddBingApplicationAsync<BaseBridgeModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        state.Events.ShouldBe(new[]
        {
            "new-pre-config", "old-pre-config", "new-post-config", "old-post-config",
            "new-init", "old-async-init", "new-stop", "old-async-stop"
        });
    }

    /// <summary>
    /// 未显式调用基类时新钩子替代对应旧接口。
    /// </summary>
    [Fact]
    public async Task NewHooksWithoutBase_ReplaceOldInterfaces()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        await services.AddBingApplicationAsync<NoBaseBridgeModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildServiceProvider();
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        state.Events.ShouldBe(new[] { "new-pre-config", "new-init", "new-stop" });
    }

    /// <summary>
    /// 隐式接口映射到同一虚钩子时，调用基类不会递归。
    /// </summary>
    [Fact]
    public void ImplicitInterfaceMapping_BaseCallDoesNotReenterHook()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<ImplicitPreModule>(options => options.AutoRegisterServices = false);
        state.Events.ShouldBe(new[] { "implicit-pre" });
    }

    /// <summary>
    /// 同步关闭钩子显式调用基类时续接旧同步接口。
    /// </summary>
    [Fact]
    public void SyncShutdownBaseCall_BridgesOldInterfaceOnce()
    {
        var state = new ReviewState();
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<SyncShutdownBridgeModule>(options => options.AutoRegisterServices = false);
        using var provider = services.BuildServiceProvider();
        provider.UseBing();
        provider.ShutdownBing();
        state.Events.ShouldBe(new[] { "new-sync-stop", "old-sync-stop" });
    }

    /// <summary>
    /// 记录阶段执行、资源释放与测试控制状态。
    /// </summary>
    public sealed class ReviewState
    {
        /// <summary>
        /// 执行事件。
        /// </summary>
        public List<string> Events { get; } = new();
        /// <summary>
        /// 关闭依赖模块的次数。
        /// </summary>
        public int DependencyShutdowns { get; set; }
        /// <summary>
        /// 关闭使用方模块的次数。
        /// </summary>
        public int ConsumerShutdowns { get; set; }
        /// <summary>
        /// 释放模块的次数。
        /// </summary>
        public int Disposals { get; set; }
        /// <summary>
        /// 是否在后置初始化抛错。
        /// </summary>
        public bool FailPost { get; set; }
        /// <summary>
        /// 配置取消来源。
        /// </summary>
        public CancellationTokenSource Cancellation { get; set; }
        /// <summary>
        /// 触发取消的配置阶段。
        /// </summary>
        public string CancelPhase { get; set; }
        /// <summary>
        /// 异步释放已开始的信号。
        /// </summary>
        public TaskCompletionSource<bool> DisposeEntered { get; set; }
        /// <summary>
        /// 允许异步释放完成的信号。
        /// </summary>
        public TaskCompletionSource<bool> DisposeRelease { get; set; }
    }

    /// <summary>
    /// 没有前置初始化钩子的依赖模块。
    /// </summary>
    public class DependencyModule : BingModule
    {
        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("init-dependency");
        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context)
        {
            var state = context.ServiceProvider.GetRequiredService<ReviewState>();
            state.Events.Add("stop-dependency");
            state.DependencyShutdowns++;
        }
    }

    /// <summary>
    /// 具有前置钩子的依赖方。
    /// </summary>
    [DependsOnModule(typeof(DependencyModule))]
    public class ConsumerModule : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("pre-consumer");
        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("init-consumer");
        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context)
        {
            if (context.ServiceProvider.GetRequiredService<ReviewState>().FailPost)
                throw new InvalidOperationException("post failed");
        }
        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context)
        {
            var state = context.ServiceProvider.GetRequiredService<ReviewState>();
            state.Events.Add("stop-consumer");
            state.ConsumerShutdowns++;
        }
    }

    /// <summary>
    /// 含实际异步配置重写的中间基类。
    /// </summary>
    public abstract class AsyncConfigurationBase : BingModule
    {
        /// <inheritdoc />
        public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            ((ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance)
                .Events.Add("ancestor-config");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 以同名方法隐藏异步配置的派生类。
    /// </summary>
    public class HiddenConfigurationModule : AsyncConfigurationBase
    {
        /// <summary>
        /// 仅用于验证隐藏成员不会替代虚钩子。
        /// </summary>
        public new Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            ((ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance)
                .Events.Add("hidden-config");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 含实际异步初始化重写的中间基类。
    /// </summary>
    public abstract class AsyncInitializationBase : BingModule
    {
        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("ancestor-init");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 隐藏异步初始化成员的派生类。
    /// </summary>
    public class HiddenInitializationModule : AsyncInitializationBase
    {
        /// <summary>
        /// 仅用于验证隐藏成员不会替代虚钩子。
        /// </summary>
        public new Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("hidden-init");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 前置和后置异步钩子的中间基类。
    /// </summary>
    public abstract class AsyncPrePostBase : BingModule
    {
        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("ancestor-pre");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("ancestor-post");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 同名隐藏前置和后置异步方法的派生模块。
    /// </summary>
    public class HiddenPrePostModule : AsyncPrePostBase
    {
        /// <summary>
        /// 仅用于验证前置隐藏成员不参与分派。
        /// </summary>
        public new Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("hidden-pre");
            return Task.CompletedTask;
        }
        /// <summary>
        /// 仅用于验证后置隐藏成员不参与分派。
        /// </summary>
        public new Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("hidden-post");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 同步入口预检前不应执行的模块。
    /// </summary>
    public class PreSideEffectModule : BingModule
    {
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("pre-side-effect");
    }

    /// <summary>
    /// 含实际异步关闭重写的中间基类。
    /// </summary>
    public abstract class AsyncShutdownBase : BingModule
    {
        /// <inheritdoc />
        public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("ancestor-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 隐藏异步关闭成员的派生类。
    /// </summary>
    public class HiddenShutdownModule : AsyncShutdownBase
    {
        /// <summary>
        /// 仅用于验证隐藏成员不会替代虚钩子。
        /// </summary>
        public new Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("hidden-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 完成回调后主动取消注册的模块。
    /// </summary>
    public class CancelingConfigurationModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private ReviewState _state;

        /// <inheritdoc />
        public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            Visit(services, "PreConfigureServices");
        /// <inheritdoc />
        public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            Visit(services, "ConfigureServices");
        /// <inheritdoc />
        public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
            Visit(services, "PostConfigureServices");

        /// <summary>
        /// 记录配置并按需要取消。
        /// </summary>
        private Task Visit(IServiceCollection services, string phase)
        {
            _state = (ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance;
            _state.Events.Add(phase);
            if (_state.CancelPhase == phase) _state.Cancellation.Cancel();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _state.DisposeEntered?.TrySetResult(true);
            if (_state.DisposeRelease != null)
                await _state.DisposeRelease.Task;
            _state.Disposals++;
        }
    }

    /// <summary>
    /// 显式旧接口与新钩子并存的模块。
    /// </summary>
    public class BaseBridgeModule : BingModule, IBingPreConfigureServices, IBingPostConfigureServices,
        IBingAsyncModuleInitializer, IBingAsyncModuleShutdown
    {
        /// <summary>
        /// 记录注册阶段事件。
        /// </summary>
        private static void Record(IServiceCollection services, string value) =>
            ((ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance)
            .Events.Add(value);

        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services)
        {
            Record(services, "new-pre-config");
            base.PreConfigureServices(services);
        }
        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services)
        {
            Record(services, "new-post-config");
            base.PostConfigureServices(services);
        }
        /// <inheritdoc />
        public override async Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("new-init");
            await base.OnApplicationInitializationAsync(context, cancellationToken);
        }
        /// <inheritdoc />
        public override async Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("new-stop");
            await base.OnApplicationShutdownAsync(context, cancellationToken);
        }
        void IBingPreConfigureServices.PreConfigureServices(IServiceCollection services)
        {
            Record(services, "old-pre-config");
            base.PreConfigureServices(services);
        }
        void IBingPostConfigureServices.PostConfigureServices(IServiceCollection services)
        {
            Record(services, "old-post-config");
            base.PostConfigureServices(services);
        }
        async Task IBingAsyncModuleInitializer.InitializeAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("old-async-init");
            await base.OnApplicationInitializationAsync(context, cancellationToken);
        }
        async Task IBingAsyncModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("old-async-stop");
            await base.OnApplicationShutdownAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// 新钩子不调用基类的模块。
    /// </summary>
    public class NoBaseBridgeModule : BaseBridgeModule
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) =>
            ((ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance)
            .Events.Add("new-pre-config");
        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) { }
        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("new-init");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("new-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 由同一个重写隐式实现旧前置配置接口的模块。
    /// </summary>
    public class ImplicitPreModule : BingModule, IBingPreConfigureServices
    {
        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services)
        {
            ((ReviewState)services.First(x => x.ServiceType == typeof(ReviewState)).ImplementationInstance)
                .Events.Add("implicit-pre");
            base.PreConfigureServices(services);
        }
    }

    /// <summary>
    /// 新同步关闭钩子调用旧同步关闭接口的模块。
    /// </summary>
    public class SyncShutdownBridgeModule : BingModule, IBingModuleShutdown
    {
        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("new-sync-stop");
            base.OnApplicationShutdown(context);
        }
        void IBingModuleShutdown.Shutdown(BingModuleShutdownContext context)
        {
            context.ServiceProvider.GetRequiredService<ReviewState>().Events.Add("old-sync-stop");
            base.OnApplicationShutdown(context);
        }
    }
}
