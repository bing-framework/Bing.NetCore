using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 验证根容器释放与模块关闭并发时的等待行为。
/// </summary>
[Collection("Module static compatibility")]
public class ModuleConcurrentDisposalTest
{
    /// <summary>
    /// 验证并发异步关闭共享同一次模块清理。
    /// </summary>
    [Fact]
    public async Task ShutdownAsync_ConcurrentCalls_ShouldCloseOnce()
    {
        var gate = new ShutdownGate();
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddBingApplication<GatedShutdownModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        await provider.UseBingAsync();

        var first = provider.ShutdownBingAsync();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = provider.ShutdownBingAsync();
        var disposing = provider.DisposeAsync().AsTask();
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.False(disposing.IsCompleted);
        Assert.Equal(0, gate.Disposals);

        gate.Release.TrySetResult(true);
        await Task.WhenAll(first, second, disposing).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, gate.Shutdowns);
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证同步释放等待已开始的异步关闭而不重复执行钩子。
    /// </summary>
    [Fact]
    public async Task Dispose_DuringAsyncShutdown_ShouldWaitAndCloseOnce()
    {
        var gate = new ShutdownGate();
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddBingApplication<GatedShutdownModule>(options => options.AutoRegisterServices = false);
        var provider = services.BuildBingServiceProvider();
        await provider.UseBingAsync();

        var shutdown = provider.ShutdownBingAsync();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispose = Task.Run(provider.Dispose);
        try
        {
            await Task.Delay(100);
            Assert.False(dispose.IsCompleted);
            Assert.Equal(0, gate.Disposals);
        }
        finally { gate.Release.TrySetResult(true); }

        await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, gate.Shutdowns);
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 控制关闭钩子等待的状态。
    /// </summary>
    private sealed class ShutdownGate
    {
        /// <summary>
        /// 获取钩子进入信号。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取钩子放行信号。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取关闭调用次数。
        /// </summary>
        public int Shutdowns;

        /// <summary>
        /// 获取资源释放次数。
        /// </summary>
        public int Disposals;
    }

    /// <summary>
    /// 在关闭时等待信号的模块。
    /// </summary>
    private sealed class GatedShutdownModule : BingModule, IDisposable
    {
        /// <summary>
        /// 测试关闭门。
        /// </summary>
        private ShutdownGate _gate;

        /// <inheritdoc />
        public override void OnApplicationInitialization(BingModuleInitializationContext context) =>
            _gate = context.ServiceProvider.GetRequiredService<ShutdownGate>();

        /// <inheritdoc />
        public override async Task OnApplicationShutdownAsync(BingModuleShutdownContext context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _gate.Shutdowns);
            _gate.Entered.TrySetResult(true);
            await _gate.Release.Task;
        }

        /// <inheritdoc />
        public void Dispose() => Interlocked.Increment(ref _gate.Disposals);
    }
}
