using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Tests.Modularity;

/// <summary>
/// 验证独立异步接口跨越真实等待边界时的阶段与资源行为。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleAsyncPhaseBoundaryTest
{
    /// <summary>
    /// 每个异步接口完成前都阻止后续阶段执行。
    /// </summary>
    [Theory]
    [InlineData("config-pre")]
    [InlineData("config-post")]
    [InlineData("init-pre")]
    [InlineData("init-post")]
    public async Task AsyncInterface_WaitsBeforeRunningFollowingPhase(string phase)
    {
        var gate = new PhaseGate(phase);
        var services = new ServiceCollection().AddSingleton(gate);
        if (phase.StartsWith("config-", StringComparison.Ordinal))
        {
            var registration = services.AddBingApplicationAsync<GatedModule>(o => o.AutoRegisterServices = false);
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                registration.IsCompleted.ShouldBeFalse();
                gate.Events.Last().ShouldBe(phase);
            }
            finally { gate.Release.TrySetResult(true); }
            await registration;
        }
        else
            await services.AddBingApplicationAsync<GatedModule>(o => o.AutoRegisterServices = false);

        await using var provider = services.BuildServiceProvider();
        if (phase.StartsWith("init-", StringComparison.Ordinal))
        {
            var initialization = provider.UseBingAsync();
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                initialization.IsCompleted.ShouldBeFalse();
                gate.Events.Last().ShouldBe(phase);
            }
            finally { gate.Release.TrySetResult(true); }
            await initialization;
        }
        else await provider.UseBingAsync();

        gate.Events.ShouldBe(new[] { "config-pre", "config-post", "init-pre", "init-main", "init-post" });
        await provider.ShutdownBingAsync();
        gate.Events.Last().ShouldBe("shutdown");
        gate.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 异步等待后的普通故障保留原始异常与阶段诊断。
    /// </summary>
    [Theory]
    [InlineData("config-pre")]
    [InlineData("config-post")]
    [InlineData("init-pre")]
    [InlineData("init-post")]
    public async Task AsyncInterface_FailureAfterAwaitPreservesOriginalException(string phase)
    {
        var gate = new PhaseGate(phase)
        {
            Failure = new InvalidOperationException("delayed-" + phase)
        };
        var services = new ServiceCollection().AddSingleton(gate);
        if (phase.StartsWith("config-", StringComparison.Ordinal))
        {
            var registration = services.AddBingApplicationAsync<GatedModule>(o => o.AutoRegisterServices = false);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            registration.IsCompleted.ShouldBeFalse();
            gate.Release.TrySetResult(true);
            var error = await Should.ThrowAsync<InvalidOperationException>(() => registration);
            ReferenceEquals(error, gate.Failure).ShouldBeTrue();
            error.Data["Bing.ModulePhase"].ShouldBe(phase == "config-pre" ?
                "PreConfigureServices" : "PostConfigureServices");
        }
        else
        {
            await services.AddBingApplicationAsync<GatedModule>(o => o.AutoRegisterServices = false);
            await using var provider = services.BuildServiceProvider();
            var initialization = provider.UseBingAsync();
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            initialization.IsCompleted.ShouldBeFalse();
            gate.Release.TrySetResult(true);
            var error = await Should.ThrowAsync<InvalidOperationException>(() => initialization);
            ReferenceEquals(error, gate.Failure).ShouldBeTrue();
            error.Data["Bing.ModulePhase"].ShouldBe(phase == "init-pre" ? "PreInitialize" : "PostInitialize");
            gate.Events.Last().ShouldBe("shutdown");
        }
        gate.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 调用方取消正在等待的独立接口时停止后续阶段并释放模块。
    /// </summary>
    [Theory]
    [InlineData("config-pre")]
    [InlineData("config-post")]
    [InlineData("init-pre")]
    [InlineData("init-post")]
    public async Task AsyncInterface_CallerCancellationStopsFollowingPhase(string phase)
    {
        var gate = new PhaseGate(phase);
        var services = new ServiceCollection().AddSingleton(gate);
        using var cancellation = new CancellationTokenSource();
        if (phase.StartsWith("config-", StringComparison.Ordinal))
        {
            var registration = services.AddBingApplicationAsync<GatedModule>(
                o => o.AutoRegisterServices = false, cancellation.Token);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            registration.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();
            var error = await Should.ThrowAsync<OperationCanceledException>(() => registration);
            error.CancellationToken.ShouldBe(cancellation.Token);
            Should.Throw<InvalidOperationException>(() => services.AddBingApplication<GatedModule>());
        }
        else
        {
            await services.AddBingApplicationAsync<GatedModule>(o => o.AutoRegisterServices = false);
            await using var provider = services.BuildServiceProvider();
            var initialization = provider.UseBingAsync(cancellation.Token);
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            initialization.IsCompleted.ShouldBeFalse();
            cancellation.Cancel();
            var error = await CaptureCancellationAsync(initialization);
            error.CancellationToken.ShouldBe(gate.SeenToken);
            error.Data["Bing.ModulePhase"].ShouldBe(phase == "init-pre" ? "PreInitialize" : "PostInitialize");
            gate.Events.Last().ShouldBe("shutdown");
        }
        gate.SeenToken.CanBeCanceled.ShouldBeTrue();
        if (phase != "init-post") gate.Events.ShouldNotContain("init-post");
        if (phase != "init-pre" && phase != "init-post") gate.Events.ShouldNotContain("init-pre");
        gate.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// 直接等待任务并获取其发布的取消异常。
    /// </summary>
    private static async Task<OperationCanceledException> CaptureCancellationAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException error) { return error; }
        throw new InvalidOperationException("预期任务以取消结束。");
    }

    /// <summary>
    /// 保存阶段门控与资源观察结果。
    /// </summary>
    public sealed class PhaseGate
    {
        /// <summary>
        /// 初始化一个 <see cref="PhaseGate" /> 类型的实例。
        /// </summary>
        /// <param name="phase">需要暂停的阶段。</param>
        public PhaseGate(string phase) => Phase = phase;

        /// <summary>
        /// 获取需要暂停的阶段。
        /// </summary>
        public string Phase { get; }

        /// <summary>
        /// 获取进入暂停阶段的信号。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取继续执行的信号。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取阶段事件。
        /// </summary>
        public List<string> Events { get; } = new();

        /// <summary>
        /// 获取阶段观察到的取消令牌。
        /// </summary>
        public CancellationToken SeenToken { get; set; }

        /// <summary>
        /// 获取等待后要抛出的故障。
        /// </summary>
        public InvalidOperationException Failure { get; set; }

        /// <summary>
        /// 获取模块释放次数。
        /// </summary>
        public int Disposals { get; set; }
    }

    /// <summary>
    /// 在指定阶段异步等待的模块。
    /// </summary>
    public sealed class GatedModule : BingModule, IPreConfigureServices, IPostConfigureServices,
        IOnPreApplicationInitialization, IOnPostApplicationInitialization,
        IBingModuleShutdown, IAsyncDisposable
    {
        /// <summary>
        /// 保存模块所属的阶段门控。
        /// </summary>
        private PhaseGate _gate;

        /// <inheritdoc />
        Task IPreConfigureServices.PreConfigureServicesAsync(IServiceCollection services,
            CancellationToken cancellationToken)
        {
            _gate = (PhaseGate)services.Last(x => x.ServiceType == typeof(PhaseGate)).ImplementationInstance;
            return RunAsync("config-pre", cancellationToken);
        }

        /// <inheritdoc />
        Task IPostConfigureServices.PostConfigureServicesAsync(IServiceCollection services,
            CancellationToken cancellationToken) => RunAsync("config-post", cancellationToken);

        /// <inheritdoc />
        Task IOnPreApplicationInitialization.OnPreApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken cancellationToken) =>
            RunAsync("init-pre", cancellationToken);

        /// <inheritdoc />
        Task IOnPostApplicationInitialization.OnPostApplicationInitializationAsync(
            BingModuleInitializationContext context, CancellationToken cancellationToken) =>
            RunAsync("init-post", cancellationToken);

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            _gate.Events.Add("init-main");

        /// <inheritdoc />
        Task IBingModuleShutdown.ShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            _gate.Events.Add("shutdown");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _gate.Disposals++;
            return default;
        }

        /// <summary>
        /// 执行并记录指定的异步阶段。
        /// </summary>
        private async Task RunAsync(string phase, CancellationToken token)
        {
            _gate.Events.Add(phase);
            if (_gate.Phase != phase) return;
            _gate.SeenToken = token;
            _gate.Entered.TrySetResult(true);
            await _gate.Release.Task.WaitAsync(token).ConfigureAwait(false);
            if (_gate.Failure != null) throw _gate.Failure;
        }
    }
}
