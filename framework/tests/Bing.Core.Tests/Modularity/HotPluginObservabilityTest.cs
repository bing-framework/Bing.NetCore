using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证热更新状态快照与失败诊断。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class HotPluginObservabilityTest
{
    /// <summary>
    /// 正常插件模块名称。
    /// </summary>
    private const string TopModule = "Bing.Core.PluginFixtures.FixtureTopModule";

    /// <summary>
    /// 配置失败插件模块名称。
    /// </summary>
    private const string ConfigurationFailureModule =
        "Bing.Core.PluginFixtures.FixtureHotPreConfigurationFailureModule";

    /// <summary>
    /// 初始化失败插件模块名称。
    /// </summary>
    private const string InitializationFailureModule =
        "Bing.Core.PluginFixtures.FixtureHotPreFailureModule";

    /// <summary>
    /// 验证同版本重载仍生成新代，且状态快照不会阻止旧上下文回收。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_SameVersion_ShouldChangeGenerationIdAndCollectOldContext()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));

        await host.ReloadAsync();
        var previousStatus = host.Status;
        var previous = previousStatus.Current;
        previous.ShouldNotBeNull();
        previous.Id.ShouldNotBe(Guid.Empty);
        previous.Plugins.Single().Id.ShouldBe("Contoso.Live");
        previous.ActiveCalls.ShouldBe(0);
        previous.IsRetiring.ShouldBeFalse();
        var previousContext = await CaptureContextAsync(host);

        await host.ReloadAsync();

        host.Status.Current.Id.ShouldNotBe(previous.Id);
        host.Status.Current.Plugins.Single().VersionText.ShouldBe("1.0.0");
        host.Status.Retiring.ShouldBeEmpty();
        previousStatus.Current.Id.ShouldBe(previous.Id);
        await AssertCollectedAsync(previousContext);
    }

    /// <summary>
    /// 验证旧代活动调用和退役等待状态可观察。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_ActiveCall_ShouldExposeRetiringGenerationUntilCallEnds()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var oldId = host.Status.Current.Id;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.RunAsync(async (_, _) =>
        {
            entered.TrySetResult(true);
            await release.Task;
            return true;
        });
        await entered.Task;
        host.Status.Current.ActiveCalls.ShouldBe(1);

        var reload = host.ReloadAsync();
        try
        {
            await WaitUntilAsync(() => host.Status.Retiring.Count == 1);
            var status = host.Status;
            status.Current.Id.ShouldNotBe(oldId);
            status.Current.ActiveCalls.ShouldBe(0);
            status.Current.State.ShouldBe(BingHotPluginGenerationState.Active);
            var retiring = status.Retiring.Single();
            retiring.Id.ShouldBe(oldId);
            retiring.ActiveCalls.ShouldBe(1);
            retiring.State.ShouldBe(BingHotPluginGenerationState.Draining);
            retiring.IsRetiring.ShouldBeTrue();
            retiring.Plugins.Single().Id.ShouldBe("Contoso.Live");
            reload.IsCompleted.ShouldBeFalse();
        }
        finally { release.TrySetResult(true); }

        (await active).ShouldBeTrue();
        await reload;
        host.Status.Retiring.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证旧代关闭钩子运行期间可以观察关闭状态。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_PendingShutdown_ShouldExposeClosingGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        var buildCount = 0;
        var shutdown = new ShutdownGate();
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options =>
            {
                options.PluginSources.AddDirectory(directory);
                options.AdditionalModules.Add(typeof(ClosingGateModule));
            },
            services => services.AddSingleton(new ShutdownGateRegistration(
                Interlocked.Increment(ref buildCount) == 1 ? shutdown : null)));
        await host.ReloadAsync();
        var oldId = host.Status.Current.Id;

        var reload = host.ReloadAsync();
        try
        {
            await shutdown.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            var status = host.Status;
            status.Current.Id.ShouldNotBe(oldId);
            status.Current.State.ShouldBe(BingHotPluginGenerationState.Active);
            var retiring = status.Retiring.Single();
            retiring.Id.ShouldBe(oldId);
            retiring.State.ShouldBe(BingHotPluginGenerationState.Closing);
            retiring.ActiveCalls.ShouldBe(0);
            reload.IsCompleted.ShouldBeFalse();
        }
        finally { shutdown.Release(); }

        await reload;
        host.Status.Retiring.ShouldBeEmpty();
    }

    /// <summary>
    /// 验证候选配置或初始化失败时诊断表明旧代仍然生效。
    /// </summary>
    [Theory]
    [InlineData(ConfigurationFailureModule)]
    [InlineData(InitializationFailureModule)]
    public async Task ReloadAsync_FailedCandidate_ShouldReportUnchangedGeneration(string moduleName)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule },
            directoryName: "live");
        var failed = workspace.CreatePlugin("Contoso.Candidate", "1.0.0", new[] { moduleName },
            directoryName: "failed");
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var previousId = host.Status.Current.Id;
        directory = failed;

        var failure = await CaptureCandidateFailureAsync(host);

        failure.AttemptId.ShouldNotBe(Guid.Empty);
        failure.PreviousGenerationId.ShouldBe(previousId);
        failure.CurrentGenerationId.ShouldBe(previousId);
        failure.Phase.ShouldBe(BingHotPluginReloadPhase.BuildCandidate);
        failure.Switched.ShouldBeFalse();
        host.Status.Current.Id.ShouldBe(previousId);
        host.Status.Retiring.ShouldBeEmpty();
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
    }

    /// <summary>
    /// 验证等待重新加载时取消不会改变当前代。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_CanceledBeforeTurn_ShouldReportUnchangedGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        var currentId = host.Status.Current.Id;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException error = null;
        try { await host.ReloadAsync(cancellation.Token); }
        catch (OperationCanceledException caught) { error = caught; }
        error.ShouldNotBeNull();
        var failure = BingHotPluginReloadFailureInfo.FromException(error);

        failure.ShouldNotBeNull();
        failure.Phase.ShouldBe(BingHotPluginReloadPhase.WaitForTurn);
        failure.Switched.ShouldBeFalse();
        failure.CurrentGenerationId.ShouldBe(currentId);
        host.Status.Current.Id.ShouldBe(currentId);
    }

    /// <summary>
    /// 验证旧代关闭失败时新代仍可服务，且诊断明确表明已切换。
    /// </summary>
    [Fact]
    public async Task ReloadAsync_OldShutdownFailure_ShouldReportSwitchedGeneration()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Live", "1.0.0", new[] { TopModule });
        var buildCount = 0;
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options =>
            {
                options.PluginSources.AddDirectory(directory);
                options.AdditionalModules.Add(typeof(ShutdownFailureModule));
            },
            services => services.AddSingleton(new ShutdownProbe(Interlocked.Increment(ref buildCount) == 1)));
        await host.ReloadAsync();
        var previousId = host.Status.Current.Id;

        var failure = await CaptureShutdownFailureAsync(host);

        failure.AttemptId.ShouldNotBe(Guid.Empty);
        failure.PreviousGenerationId.ShouldBe(previousId);
        failure.CurrentGenerationId.ShouldNotBe(previousId);
        failure.Phase.ShouldBe(BingHotPluginReloadPhase.ClosePrevious);
        failure.Switched.ShouldBeTrue();
        failure.CurrentGenerationId.ShouldBe(host.Status.Current.Id);
        host.Status.Retiring.ShouldBeEmpty();
        var current = await host.RunAsync((provider, _) => Task.FromResult(
            provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().Id));
        current.ShouldBe("Contoso.Live");
    }

    /// <summary>
    /// 在独立栈帧获取插件上下文的弱引用。
    /// </summary>
    /// <param name="host">当前插件宿主。</param>
    /// <returns>插件上下文弱引用。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<WeakReference> CaptureContextAsync(BingHotPluginHost<PluginHostModule> host) =>
        host.RunAsync((provider, _) =>
        {
            var assembly = provider.GetRequiredService<IBingPluginContainer>().Plugins.Single().EntryAssembly;
            return Task.FromResult(new WeakReference(AssemblyLoadContext.GetLoadContext(assembly)));
        });

    /// <summary>
    /// 在独立栈帧读取候选失败诊断。
    /// </summary>
    /// <param name="host">当前插件宿主。</param>
    /// <returns>候选失败诊断。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<BingHotPluginReloadFailureInfo> CaptureCandidateFailureAsync(
        BingHotPluginHost<PluginHostModule> host)
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(() => host.ReloadAsync());
        return BingHotPluginReloadFailureInfo.FromException(error);
    }

    /// <summary>
    /// 在独立栈帧读取旧代关闭失败诊断。
    /// </summary>
    /// <param name="host">当前插件宿主。</param>
    /// <returns>旧代关闭失败诊断。</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<BingHotPluginReloadFailureInfo> CaptureShutdownFailureAsync(
        BingHotPluginHost<PluginHostModule> host)
    {
        var error = await Should.ThrowAsync<AggregateException>(() => host.ReloadAsync());
        return BingHotPluginReloadFailureInfo.FromException(error);
    }

    /// <summary>
    /// 等待状态条件成立。
    /// </summary>
    /// <param name="condition">待满足的条件。</param>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    /// <summary>
    /// 验证插件加载上下文可回收。
    /// </summary>
    /// <param name="reference">上下文弱引用。</param>
    private static async Task AssertCollectedAsync(WeakReference reference)
    {
        for (var attempt = 0; attempt < 20 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (reference.IsAlive) await Task.Delay(25);
        }
        reference.IsAlive.ShouldBeFalse();
    }

}

/// <summary>
/// 标记对应代是否应在关闭时失败。
/// </summary>
public sealed class ShutdownProbe
{
    /// <summary>
    /// 初始化一个 <see cref="ShutdownProbe" /> 类型的实例。
    /// </summary>
    /// <param name="fail">是否在关闭时失败。</param>
    public ShutdownProbe(bool fail) => Fail = fail;

    /// <summary>
    /// 获取是否在关闭时失败。
    /// </summary>
    public bool Fail { get; }
}

/// <summary>
/// 按所在代的配置模拟关闭失败。
/// </summary>
public sealed class ShutdownFailureModule : BingModule
{
    /// <inheritdoc />
    public override void OnApplicationShutdown(BingModuleShutdownContext context)
    {
        if (context.ServiceProvider.GetRequiredService<ShutdownProbe>().Fail)
            throw new InvalidOperationException("previous generation shutdown failed");
    }
}

/// <summary>
/// 控制旧代关闭钩子的完成时间。
/// </summary>
public sealed class ShutdownGate
{
    /// <summary>
    /// 关闭钩子进入信号。
    /// </summary>
    private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 关闭钩子释放信号。
    /// </summary>
    private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// 获取关闭钩子进入信号。
    /// </summary>
    public Task Entered => _entered.Task;

    /// <summary>
    /// 等待测试释放关闭钩子。
    /// </summary>
    public Task WaitAsync()
    {
        _entered.TrySetResult(true);
        return _release.Task;
    }

    /// <summary>
    /// 释放关闭钩子。
    /// </summary>
    public void Release() => _release.TrySetResult(true);
}

/// <summary>
/// 保存本代关闭钩子使用的门闩。
/// </summary>
public sealed class ShutdownGateRegistration
{
    /// <summary>
    /// 初始化一个 <see cref="ShutdownGateRegistration" /> 类型的实例。
    /// </summary>
    /// <param name="gate">本代关闭门闩。</param>
    public ShutdownGateRegistration(ShutdownGate gate) => Gate = gate;

    /// <summary>
    /// 获取本代关闭门闩。
    /// </summary>
    public ShutdownGate Gate { get; }
}

/// <summary>
/// 通过门闩延后模块关闭。
/// </summary>
public sealed class ClosingGateModule : BingModule
{
    /// <inheritdoc />
    public override Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
        CancellationToken cancellationToken) =>
        context.ServiceProvider.GetRequiredService<ShutdownGateRegistration>().Gate?.WaitAsync() ?? Task.CompletedTask;
}
