using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证旧接口桥接与异步取消诊断。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleLifecycleReviewRound18Test
{
    /// <summary>
    /// 直接分派旧接口时调用基类仍只执行一次。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyInterfacesCallingBase_RunOnce(bool asynchronousShutdown)
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<ExplicitLegacyModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();

        await provider.UseBingAsync();
        if (asynchronousShutdown)
            await provider.ShutdownBingAsync();
        else
            provider.ShutdownBing();

        events.ShouldBe(new[]
        {
            "old-pre", "old-post", "old-init", asynchronousShutdown ? "old-async-stop" : "old-sync-stop"
        });
    }

    /// <summary>
    /// 隐式旧接口使用同名隐藏方法时，基类调用不重复进入接口。
    /// </summary>
    [Fact]
    public void ImplicitLegacyInterfacesCallingBase_RunOnce()
    {
        var events = new List<string>();
        var services = new ServiceCollection().AddSingleton(events);
        services.AddBingApplication<ImplicitLegacyModule>(options => options.AutoRegisterServices = false);

        events.ShouldBe(new[] { "implicit-pre", "implicit-post" });
    }

    /// <summary>
    /// 旧接口抛错后基类桥接状态应恢复。
    /// </summary>
    [Fact]
    public void LegacyInterfaceFailure_DoesNotLeaveBridgeActive()
    {
        var module = new ThrowOnceLegacyModule();
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => module.PreConfigureServices(services));
        module.PreConfigureServices(services);

        module.Calls.ShouldBe(2);
    }

    /// <summary>
    /// 三阶段取消保留原异常、模块诊断并清理一次。
    /// </summary>
    [Theory]
    [InlineData("PreInitialize")]
    [InlineData("Initialize")]
    [InlineData("PostInitialize")]
    public async Task InitializationCancellation_PreservesOriginalException(string canceledPhase)
    {
        using var cancellation = new CancellationTokenSource();
        var state = new CancellationState { CanceledPhase = canceledPhase, Source = cancellation };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<CancelingModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();

        var pending = provider.UseBingAsync(cancellation.Token);
        var error = await CaptureCancellationAsync(pending);

        error.ShouldBeSameAs(state.Original);
        error.Message.ShouldBe("canceled at " + canceledPhase);
        error.CancellationToken.ShouldBe(cancellation.Token);
        error.Data["Origin"].ShouldBe("module");
        error.Data["Bing.ModulePhase"].ShouldBe(canceledPhase);
        error.Data["Bing.ModuleType"].ShouldBe(typeof(CancelingModule).FullName);
        pending.IsCanceled.ShouldBeTrue();
        pending.IsFaulted.ShouldBeFalse();
        state.Events.Last().ShouldBe("shutdown");
        state.Events.Count.ShouldBe(Array.IndexOf(new[]
            { "PreInitialize", "Initialize", "PostInitialize" }, canceledPhase) + 2);
        state.Shutdowns.ShouldBe(1);
        state.Disposals.ShouldBe(1);
        await Should.ThrowAsync<InvalidOperationException>(() => provider.UseBingAsync());
    }

    /// <summary>
    /// 取消时关闭失败仍附加到原始异常并继续释放资源。
    /// </summary>
    [Fact]
    public async Task InitializationCancellation_WithShutdownError_PreservesCauseAndCleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new CancellationState
        {
            CanceledPhase = "Initialize", Source = cancellation, FailShutdown = true
        };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<CancelingModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();

        var pending = provider.UseBingAsync(cancellation.Token);
        var error = await CaptureCancellationAsync(pending);

        error.ShouldBeSameAs(state.Original);
        error.Data["Bing.ModulePhase"].ShouldBe("Initialize");
        var cleanup = error.Data["Bing.ModuleCleanupErrors"].ShouldBeOfType<AggregateException>();
        cleanup.InnerExceptions.Single().Message.ShouldBe("shutdown failed");
        pending.IsCanceled.ShouldBeTrue();
        pending.IsFaulted.ShouldBeFalse();
        state.Shutdowns.ShouldBe(1);
        state.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 调用前取消仍返回已取消任务且不进入模块钩子。
    /// </summary>
    [Fact]
    public async Task PreCanceledInitialization_KeepsCanceledTaskWithoutStartingModules()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = new CancellationState { Source = cancellation };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<CancelingModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();

        var pending = provider.UseBingAsync(cancellation.Token);

        (await CaptureCancellationAsync(pending)).CancellationToken.ShouldBe(cancellation.Token);
        pending.IsCanceled.ShouldBeTrue();
        pending.IsFaulted.ShouldBeFalse();
        state.Events.ShouldBeEmpty();
    }

    /// <summary>
    /// 并发非 Web 初始化调用共享含原始取消原因的任务。
    /// </summary>
    [Fact]
    public async Task ConcurrentInitializationCancellation_SharesOriginalException()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new CancellationState
        {
            CanceledPhase = "PreInitialize",
            Source = cancellation,
            Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
            Continue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var services = new ServiceCollection().AddSingleton(state);
        services.AddBingApplication<CancelingModule>(options => options.AutoRegisterServices = false);
        await using var provider = services.BuildBingServiceProvider();

        var manager = provider.GetRequiredService<IBingModuleManager>();
        var first = manager.InitializeAsync(cancellationToken: cancellation.Token);
        await state.Entered.Task;
        var second = manager.InitializeAsync(cancellationToken: cancellation.Token);
        second.ShouldBeSameAs(first);
        state.Continue.TrySetResult(true);

        (await CaptureCancellationAsync(first)).ShouldBeSameAs(state.Original);
        (await CaptureCancellationAsync(second)).ShouldBeSameAs(state.Original);
        first.IsCanceled.ShouldBeTrue();
        first.IsFaulted.ShouldBeFalse();
        state.Shutdowns.ShouldBe(1);
        state.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 直接等待任务以捕获运行时发布的取消异常。
    /// </summary>
    /// <param name="task">预期以取消结束的任务。</param>
    /// <returns>任务发布的取消异常。</returns>
    private static async Task<OperationCanceledException> CaptureCancellationAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException error) { return error; }
        throw new InvalidOperationException("初始化应以取消结束。");
    }

    /// <summary>
    /// 同一模块的新异步重写优先于同步重写和旧显式接口。
    /// </summary>
    [Fact]
    public async Task CombinedOverrides_AsyncWinsAndSyncPreflightHasNoSideEffects()
    {
        var events = new List<string>();
        var rejected = new ServiceCollection().AddSingleton(events);
        Should.Throw<InvalidOperationException>(() => rejected.AddBingApplication<CombinedOverridesModule>(
            options => options.AutoRegisterServices = false)).Message.ShouldContain("AddBingApplicationAsync");
        events.ShouldBeEmpty();

        var services = new ServiceCollection().AddSingleton(events);
        await services.AddBingApplicationAsync<CombinedOverridesModule>(
            options => options.AutoRegisterServices = false);
        events.ShouldBe(new[] { "async-pre-config", "async-config", "async-post-config" });

        await using var provider = services.BuildBingServiceProvider();
        Should.Throw<InvalidOperationException>(() => provider.UseBing()).Message.ShouldContain("UseBingAsync");
        events.Count.ShouldBe(3);
        await provider.UseBingAsync();
        await provider.ShutdownBingAsync();
        events.ShouldBe(new[]
        {
            "async-pre-config", "async-config", "async-post-config",
            "async-pre-init", "async-init", "async-post-init", "async-stop"
        });
    }

    /// <summary>
    /// 同时实现新旧生命周期契约的模块。
    /// </summary>
    public class CombinedOverridesModule : BingModule, IBingPreConfigureServices, IBingPostConfigureServices,
        IBingAsyncModuleInitializer, IBingModuleShutdown, IBingAsyncModuleShutdown
    {
        /// <summary>
        /// 记录配置事件。
        /// </summary>
        private static void Record(IServiceCollection services, string value) =>
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance).Add(value);

        /// <inheritdoc />
        public override void PreConfigureServices(IServiceCollection services) => Record(services, "sync-pre-config");
        /// <inheritdoc />
        public override Task PreConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            Record(services, "async-pre-config");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            Record(services, "old-config");
            return services;
        }
        /// <inheritdoc />
        public override void ConfigureServices(IServiceCollection services) => Record(services, "sync-config");
        /// <inheritdoc />
        public override Task ConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            Record(services, "async-config");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override void PostConfigureServices(IServiceCollection services) => Record(services, "sync-post-config");
        /// <inheritdoc />
        public override Task PostConfigureServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
        {
            Record(services, "async-post-config");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override void OnPreApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-pre-init");
        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-pre-init");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-init");
        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-init");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override void OnPostApplicationInitialization(BingModuleInitializationContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-post-init");
        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-post-init");
            return Task.CompletedTask;
        }
        /// <inheritdoc />
        public override void OnApplicationShutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("sync-stop");
        /// <inheritdoc />
        public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("async-stop");
            return Task.CompletedTask;
        }

        void IBingPreConfigureServices.PreConfigureServices(IServiceCollection services) => Record(services, "old-pre-config");
        void IBingPostConfigureServices.PostConfigureServices(IServiceCollection services) => Record(services, "old-post-config");
        Task IBingAsyncModuleInitializer.InitializeAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-init");
            return Task.CompletedTask;
        }
        void IBingModuleShutdown.Shutdown(BingModuleShutdownContext context) =>
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-sync-stop");
        Task IBingAsyncModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-async-stop");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 旧接口执行期间记录事件。
    /// </summary>
    public class ExplicitLegacyModule : BingModule, IBingPreConfigureServices, IBingPostConfigureServices,
        IBingAsyncModuleInitializer, IBingModuleShutdown, IBingAsyncModuleShutdown
    {
        void IBingPreConfigureServices.PreConfigureServices(IServiceCollection services)
        {
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance)
                .Add("old-pre");
            base.PreConfigureServices(services);
        }

        void IBingPostConfigureServices.PostConfigureServices(IServiceCollection services)
        {
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance)
                .Add("old-post");
            base.PostConfigureServices(services);
        }

        async Task IBingAsyncModuleInitializer.InitializeAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-init");
            await base.OnApplicationInitializationAsync(context, cancellationToken);
        }

        void IBingModuleShutdown.Shutdown(BingModuleShutdownContext context)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-sync-stop");
            base.OnApplicationShutdown(context);
        }

        async Task IBingAsyncModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            context.ServiceProvider.GetRequiredService<List<string>>().Add("old-async-stop");
            await base.OnApplicationShutdownAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// 使用同名隐藏方法隐式实现旧配置接口。
    /// </summary>
    public class ImplicitLegacyModule : BingModule, IBingPreConfigureServices, IBingPostConfigureServices
    {
        /// <summary>
        /// 执行旧前置配置。
        /// </summary>
        public new void PreConfigureServices(IServiceCollection services)
        {
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance)
                .Add("implicit-pre");
            base.PreConfigureServices(services);
        }

        /// <summary>
        /// 执行旧后置配置。
        /// </summary>
        public new void PostConfigureServices(IServiceCollection services)
        {
            ((List<string>)services.First(x => x.ServiceType == typeof(List<string>)).ImplementationInstance)
                .Add("implicit-post");
            base.PostConfigureServices(services);
        }
    }

    /// <summary>
    /// 首次调用会抛错的旧前置接口模块。
    /// </summary>
    public class ThrowOnceLegacyModule : BingModule, IBingPreConfigureServices
    {
        /// <summary>
        /// 旧回调进入次数。
        /// </summary>
        public int Calls { get; private set; }

        void IBingPreConfigureServices.PreConfigureServices(IServiceCollection services)
        {
            if (++Calls == 1) throw new InvalidOperationException("first call failed");
            base.PreConfigureServices(services);
        }
    }

    /// <summary>
    /// 取消阶段与清理结果。
    /// </summary>
    public sealed class CancellationState
    {
        /// <summary>
        /// 指定取消的初始化阶段。
        /// </summary>
        public string CanceledPhase { get; set; }
        /// <summary>
        /// 触发取消的令牌源。
        /// </summary>
        public CancellationTokenSource Source { get; set; }
        /// <summary>
        /// 模块抛出的原始异常。
        /// </summary>
        public OperationCanceledException Original { get; set; }
        /// <summary>
        /// 已执行的阶段。
        /// </summary>
        public List<string> Events { get; } = new();
        /// <summary>
        /// 关闭次数。
        /// </summary>
        public int Shutdowns { get; set; }
        /// <summary>
        /// 释放次数。
        /// </summary>
        public int Disposals { get; set; }
        /// <summary>
        /// 是否让关闭钩子失败。
        /// </summary>
        public bool FailShutdown { get; set; }
        /// <summary>
        /// 进入取消阶段的信号。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; set; }
        /// <summary>
        /// 允许取消阶段继续的信号。
        /// </summary>
        public TaskCompletionSource<bool> Continue { get; set; }
    }

    /// <summary>
    /// 在指定初始化阶段取消的模块。
    /// </summary>
    public class CancelingModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 测试状态。
        /// </summary>
        private CancellationState _state;

        /// <inheritdoc />
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            _state = (CancellationState)services.First(x => x.ServiceType == typeof(CancellationState))
                .ImplementationInstance;
            return services;
        }

        /// <inheritdoc />
        public override Task OnPreApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "PreInitialize");

        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "Initialize");

        /// <inheritdoc />
        public override Task OnPostApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken) => VisitAsync(context, "PostInitialize");

        /// <summary>
        /// 记录阶段并抛出携带诊断的取消异常。
        /// </summary>
        private async Task VisitAsync(BingModuleInitializationContext context, string phase)
        {
            _state = context.ServiceProvider.GetRequiredService<CancellationState>();
            _state.Events.Add(phase);
            if (_state.CanceledPhase != phase) return;
            _state.Entered?.TrySetResult(true);
            if (_state.Continue != null) await _state.Continue.Task;
            _state.Source.Cancel();
            var error = new OperationCanceledException("canceled at " + phase, _state.Source.Token);
            error.Data["Origin"] = "module";
            _state.Original = error;
            throw error;
        }

        /// <inheritdoc />
        public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            _state.Shutdowns++;
            _state.Events.Add("shutdown");
            if (_state.FailShutdown) throw new InvalidOperationException("shutdown failed");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _state.Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
