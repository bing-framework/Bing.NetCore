using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 记录模块与插件重复运行时的托管资源变化。
/// </summary>
[Collection("Bing启动期插件")]
public class ModuleResourceProbeTest
{
    /// <summary>
    /// 预热次数。
    /// </summary>
    private const int WarmupCount = 20;

    /// <summary>
    /// 测量次数。
    /// </summary>
    private const int MeasurementCount = 100;

    /// <summary>
    /// 测试输出。
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 初始化一个 <see cref="ModuleResourceProbeTest" /> 类型的实例。
    /// </summary>
    public ModuleResourceProbeTest(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// 测量正常、初始化失败和取消场景中的对象回收及释放次数。
    /// </summary>
    [Theory]
    [InlineData(ProbeMode.Normal)]
    [InlineData(ProbeMode.Failure)]
    [InlineData(ProbeMode.Canceled)]
    public async Task ModuleCycles_ShouldReleaseOwnedResourcesAndReportGc(ProbeMode mode)
    {
        ProbeModule.Reset();
        for (var index = 0; index < WarmupCount; index++)
            await RunModuleCycleAsync(mode);

        ForceCollection();
        var before = Capture();
        var references = RunModuleCycles(mode);
        var after = Capture();
        ForceCollection();

        Assert.Equal(WarmupCount + MeasurementCount, ProbeModule.Disposals);
        for (var index = 0; index < references.Count; index++)
        {
            Assert.False(references[index].Services.IsAlive, $"services index={index}");
            Assert.False(references[index].Provider.IsAlive, $"provider index={index}");
            Assert.False(references[index].Module.IsAlive, $"module index={index}");
        }
        WriteMetrics(mode.ToString(), before, after);
    }

    /// <summary>
    /// 测量插件重复更新后的代回收与资源变化。
    /// </summary>
    [Fact]
    public async Task PluginReloadCycles_ShouldCollectRetiredContextsAndReportGc()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.Probe", "1.0.0",
            new[] { "Bing.Core.PluginFixtures.FixtureTopModule" });
        await using var host = new BingHotPluginHost<PluginHostModule>(
            options => options.PluginSources.AddDirectory(directory));
        await host.ReloadAsync();
        for (var index = 0; index < WarmupCount; index++)
            await host.ReloadAsync();

        ForceCollection();
        var before = Capture();
        var references = RunPluginCycles(host);
        var after = Capture();
        ForceCollection();

        Assert.Empty(host.Status.Retiring);
        for (var index = 0; index < references.Count; index++)
            await AssertCollectedAsync(references[index], $"plugin index={index}");
        WriteMetrics("PluginReload", before, after);
    }

    /// <summary>
    /// 验证初始化回调中途取消后仍会释放已创建模块资源。
    /// </summary>
    [Fact]
    public async Task ModuleInitializationCancellationDuringCallback_ShouldReleaseOwnedResources()
    {
        ProbeModule.Reset();
        ProbeModule.BeginCancellationProbe();
        var services = new ServiceCollection();
        services.AddSingleton(new ProbeModeHolder(ProbeMode.CanceledDuringInitialization));
        services.AddBingApplication<ProbeModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var initialization = provider.UseBingAsync(cancellation.Token);

        await ProbeModule.InitializationEntered.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        await provider.DisposeAsync();

        Assert.Equal(1, ProbeModule.Disposals);
    }

    /// <summary>
    /// 在同步边界完成模块测量循环，避免末次异步状态机持有样本根。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<(WeakReference Services, WeakReference Provider, WeakReference Module)>
        RunModuleCycles(ProbeMode mode)
    {
        var references = new List<(WeakReference, WeakReference, WeakReference)>();
        for (var index = 0; index < MeasurementCount; index++)
            references.Add(RunModuleCycleAsync(mode).GetAwaiter().GetResult());
        return references;
    }

    /// <summary>
    /// 在同步边界完成插件测量循环，避免额外换代掩盖末次回收结果。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference> RunPluginCycles(BingHotPluginHost<PluginHostModule> host)
    {
        var references = new List<WeakReference>();
        for (var index = 0; index < MeasurementCount; index++)
        {
            references.Add(CapturePluginContextAsync(host).GetAwaiter().GetResult());
            host.ReloadAsync().GetAwaiter().GetResult();
        }
        return references;
    }

    /// <summary>
    /// 在独立栈帧运行一个模块生命周期。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Services, WeakReference Provider, WeakReference Module)>
        RunModuleCycleAsync(ProbeMode mode)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ProbeModeHolder(mode));
        services.AddBingApplication<ProbeModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        var module = ProbeModule.LastInstance;
        try
        {
            using var cancellation = new CancellationTokenSource();
            if (mode == ProbeMode.Canceled) cancellation.Cancel();
            await provider.UseBingAsync(cancellation.Token);
            Assert.Equal(ProbeMode.Normal, mode);
            await provider.ShutdownBingAsync();
        }
        catch (InvalidOperationException error) when (mode == ProbeMode.Failure)
        {
            Assert.Equal("resource probe initialization failure", error.Message);
        }
        catch (OperationCanceledException error) when (mode == ProbeMode.Canceled)
        {
            Assert.True(error.CancellationToken.IsCancellationRequested);
        }
        finally { await provider.DisposeAsync(); }
        return (new WeakReference(services), new WeakReference(provider), module);
    }

    /// <summary>
    /// 在独立栈帧捕获当前插件上下文的弱引用。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<WeakReference> CapturePluginContextAsync(BingHotPluginHost<PluginHostModule> host) =>
        host.RunAsync((provider, _) => Task.FromResult(new WeakReference(
            AssemblyLoadContext.GetLoadContext(provider.GetRequiredService<IBingPluginContainer>()
                .Plugins.Single().EntryAssembly))));

    /// <summary>
    /// 捕获当前资源计数器。
    /// </summary>
    private static ResourceSnapshot Capture() => new(
        GC.GetTotalAllocatedBytes(true), GC.GetTotalMemory(false),
        GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
        Process.GetCurrentProcess().WorkingSet64);

    /// <summary>
    /// 在测试中触发有界回收检查。
    /// </summary>
    private static void ForceCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>
    /// 在有界重试中确认弱引用目标已经回收。
    /// </summary>
    private static async Task AssertCollectedAsync(WeakReference reference, string message)
    {
        for (var attempt = 0; attempt < 20 && reference.IsAlive; attempt++)
        {
            ForceCollection();
            if (reference.IsAlive)
                await Task.Delay(25);
        }

        Assert.False(reference.IsAlive, message);
    }

    /// <summary>
    /// 输出测量区间的资源变化。
    /// </summary>
    private void WriteMetrics(string scenario, ResourceSnapshot before, ResourceSnapshot after) =>
        _output.WriteLine($"{scenario}: sampledIterations={MeasurementCount}, " +
            $"allocatedBytes={after.Allocated - before.Allocated}, " +
            $"managedHeapBefore={before.ManagedHeap}, managedHeapAfter={after.ManagedHeap}, " +
            $"gc0={after.Gen0 - before.Gen0}, gc1={after.Gen1 - before.Gen1}, " +
            $"gc2={after.Gen2 - before.Gen2}, " +
            $"workingSetBefore={before.WorkingSet}, workingSetAfter={after.WorkingSet}");

    /// <summary>
    /// 探针场景。
    /// </summary>
    public enum ProbeMode { Normal, Failure, Canceled, CanceledDuringInitialization }

    /// <summary>
    /// 单次资源计数快照。
    /// </summary>
    private sealed record ResourceSnapshot(long Allocated, long ManagedHeap, int Gen0, int Gen1, int Gen2,
        long WorkingSet);

    /// <summary>
    /// 探针使用的异步模块。
    /// </summary>
    private sealed class ProbeModule : BingModule, IAsyncDisposable
    {
        /// <summary>
        /// 初始化回调进入通知。
        /// </summary>
        private static TaskCompletionSource<bool> _initializationEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 已释放的模块数量。
        /// </summary>
        private static int _disposals;

        /// <summary>
        /// 最近构造的模块弱引用。
        /// </summary>
        private static WeakReference _lastInstance;

        /// <summary>
        /// 初始化一个 <see cref="ProbeModule" /> 类型的实例。
        /// </summary>
        public ProbeModule() => _lastInstance = new WeakReference(this);

        /// <summary>
        /// 获取已释放的模块数量。
        /// </summary>
        public static int Disposals => Volatile.Read(ref _disposals);

        /// <summary>
        /// 获取最近构造的模块弱引用。
        /// </summary>
        public static WeakReference LastInstance => _lastInstance;

        /// <summary>
        /// 获取初始化回调已进入的通知任务。
        /// </summary>
        public static Task InitializationEntered => _initializationEntered.Task;

        /// <summary>
        /// 开始一次初始化取消探针。
        /// </summary>
        public static void BeginCancellationProbe() => _initializationEntered =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 重置释放计数。
        /// </summary>
        public static void Reset() => Volatile.Write(ref _disposals, 0);

        /// <inheritdoc />
        public override Task OnApplicationInitializationAsync(BingModuleInitializationContext context,
            CancellationToken cancellationToken)
        {
            var mode = context.ServiceProvider.GetRequiredService<ProbeModeHolder>().Mode;
            if (mode == ProbeMode.Failure)
                throw new InvalidOperationException("resource probe initialization failure");
            if (mode == ProbeMode.CanceledDuringInitialization)
            {
                _initializationEntered.TrySetResult(true);
                return WaitForCancellationAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 等待初始化取消信号。
        /// </summary>
        private static async Task WaitForCancellationAsync(CancellationToken cancellationToken) =>
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposals);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 向初始化回调传递当前场景。
    /// </summary>
    private sealed record ProbeModeHolder(ProbeMode Mode);
}
