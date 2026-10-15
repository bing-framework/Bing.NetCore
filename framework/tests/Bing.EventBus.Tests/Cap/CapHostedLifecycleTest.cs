using AspectCore.Extensions.Hosting;
using Bing.Events.Cap;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Bing.EventBus.Tests.Cap;

/// <summary>
/// 验证 CAP 启动与停止之间的资源生命周期。
/// </summary>
public sealed class CapHostedLifecycleTest
{
    /// <summary>
    /// 验证宿主等待 CAP 启动完成。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WhenProcessorIsStarting_ShouldWaitForCompletion(bool aspectCore)
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore);
        var starting = host.StartAsync();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(starting.IsCompleted);
            Assert.Equal(0, gate.Disposals);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            await starting;
            await host.StopAsync();
        }
        Assert.False(gate.CancelledDuringStart);
        Assert.Equal(1, gate.Starts);
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证停止等待活动启动结束后再释放处理器。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAsync_WhenProcessorIsStarting_ShouldNotDisposeActiveProcessor(bool aspectCore)
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        var starting = hosted.StartAsync(CancellationToken.None);
        Task stopping = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            stopping = hosted.StopAsync(CancellationToken.None);
            Assert.False(stopping.IsCompleted);
            Assert.Equal(0, gate.Disposals);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            await starting;
            if (stopping != null)
                await stopping;
        }
        Assert.False(gate.CancelledDuringStart);
        Assert.Equal(1, gate.Starts);
        Assert.Equal(1, gate.Disposals);
        await hosted.StopAsync(CancellationToken.None);
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证启动取消不会提前释放活动处理器。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartAsync_WhenCallerCancels_ShouldWaitForSafeCleanup(bool aspectCore)
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        using var cancellation = new CancellationTokenSource();
        var starting = hosted.StartAsync(cancellation.Token);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Assert.False(starting.IsCompleted);
            Assert.Equal(0, gate.Disposals);
        }
        finally
        {
            gate.Release.TrySetResult(true);
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        Assert.False(gate.CancelledDuringStart);
        Assert.Equal(1, gate.Disposals);
        await hosted.StopAsync(CancellationToken.None);
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证停止超时只结束等待而保留活动资源。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAsync_WhenWaitIsCancelled_ShouldKeepStartupAndCleanupRunning(bool aspectCore)
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        var starting = hosted.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hosted.StopAsync(cancellation.Token));
            Assert.Equal(0, gate.Disposals);
            Assert.False(starting.IsCompleted);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            await starting;
            await hosted.StopAsync(CancellationToken.None);
        }
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证同步清理阻塞时停止超时不会误报清理完成。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAsync_WhenProcessorDisposeBlocks_ShouldCancelWaitingAndFinishCleanupLater(bool aspectCore)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;
        var gate = new ProcessorGate
        {
            OnDispose = () =>
            {
                entered.TrySetResult(true);
                if (!release.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("dispose gate timed out");
                Interlocked.Increment(ref completed);
            }
        };
        gate.Release.TrySetResult(true);
        using var host = CreateHost(gate, aspectCore);
        await host.StartAsync();
        using var cancellation = new CancellationTokenSource();
        var stopping = host.StopAsync(cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            var failure = await Record.ExceptionAsync(() => stopping);
            var cancellationFailure = failure is AggregateException aggregate
                ? Assert.IsAssignableFrom<OperationCanceledException>(Assert.Single(aggregate.Flatten().InnerExceptions))
                : Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.True(cancellationFailure.CancellationToken.IsCancellationRequested);
            Assert.Equal(0, Volatile.Read(ref completed));
            Assert.Equal(1, gate.Disposals);
        }
        finally
        {
            release.Set();
            await host.StopAsync(CancellationToken.None);
        }
        Assert.Equal(1, Volatile.Read(ref completed));
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证异步释放等待启动结束并只关闭一次。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeAsync_WhenStarting_ShouldWaitAndShareCleanup(bool aspectCore)
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        var starting = hosted.StartAsync(CancellationToken.None);
        Task first = null;
        Task second = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            first = ((IAsyncDisposable)hosted).DisposeAsync().AsTask();
            second = ((IAsyncDisposable)hosted).DisposeAsync().AsTask();
            Assert.Same(first, second);
            Assert.False(first.IsCompleted);
            Assert.Equal(0, gate.Disposals);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            await starting;
            if (first != null)
                await first;
            if (second != null)
                await second;
        }
        ((IDisposable)hosted).Dispose();
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证重复注册保留其他后台服务且只注册一个 CAP 宿主。
    /// </summary>
    [Fact]
    public void AddCapEventBus_WhenRepeated_ShouldPreserveOtherServicesAndSingleCapHost()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var other = Mock.Of<IHostedService>();
        services.AddSingleton(other);
        services.AddCapEventBus(_ => { });
        services.AddCapEventBus(_ => { });
        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToArray();

        Assert.Equal(2, hosted.Length);
        Assert.Same(other, hosted[0]);
        Assert.Equal("CapHostedLifecycleService", hosted[1].GetType().Name);
    }

    /// <summary>
    /// 验证已取消的启动请求不执行 CAP 初始化。
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenAlreadyCancelled_ShouldNotStartProcessor()
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore: false);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hosted.StartAsync(cancellation.Token));

        Assert.False(gate.Entered.Task.IsCompleted);
        Assert.Equal(0, gate.Starts);
        await hosted.StopAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => hosted.StartAsync(CancellationToken.None));
    }

    /// <summary>
    /// 验证并发启动及关闭各执行一次。
    /// </summary>
    [Fact]
    public async Task StartAndStopAsync_WhenConcurrent_ShouldExecuteOnce()
    {
        var gate = new ProcessorGate();
        using var host = CreateHost(gate, aspectCore: false);
        var hosted = host.Services.GetServices<IHostedService>().Single();
        var first = hosted.StartAsync(CancellationToken.None);
        var second = hosted.StartAsync(CancellationToken.None);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, gate.Starts);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            await Task.WhenAll(first, second);
            await Task.WhenAll(hosted.StopAsync(CancellationToken.None), hosted.StopAsync(CancellationToken.None));
        }
        Assert.Equal(1, gate.Disposals);
    }

    /// <summary>
    /// 验证宿主停止时先排空 Bing CAP 回调，再完成底层 CAP 停止。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenConsumerRuntimeIsRegistered_ShouldDrainBeforeCompletion()
    {
        var runtime = new Mock<IBingCapConsumerRuntime>();
        runtime.Setup(value => value.WaitForDrainAsync(Timeout.InfiniteTimeSpan, CancellationToken.None))
            .ReturnsAsync(true);
        var gate = new ProcessorGate();
        gate.Release.TrySetResult(true);
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton(runtime.Object);
            services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
            services.EnableBingCapConsumerCancellationRetry();
            services.AddSingleton(new CapStorageMarkerService("runtime-drain-test"));
            services.AddSingleton(new CapMessageQueueMakerService("runtime-drain-test"));
            var initializer = new Mock<IStorageInitializer>();
            initializer.Setup(value => value.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton(initializer.Object);
            services.RemoveAll<IProcessingServer>();
            services.AddSingleton<IProcessingServer>(gate);
        }).Build();

        await host.StartAsync();
        await host.StopAsync();

        runtime.Verify(value => value.BeginDrain(), Times.Once);
        runtime.Verify(value => value.WaitForDrainAsync(Timeout.InfiniteTimeSpan, CancellationToken.None), Times.Once);
    }

    /// <summary>
    /// 验证有限排空预算到期时报告未完成并保留诊断。
    /// </summary>
    [Fact]
    public async Task StopAsync_WhenConfiguredDrainTimeoutExpires_ShouldReportIncomplete()
    {
        var runtime = new Mock<IBingCapConsumerRuntime>();
        runtime.Setup(value => value.WaitForDrainAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None))
            .ReturnsAsync(false);
        var gate = new ProcessorGate();
        gate.Release.TrySetResult(true);
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton(runtime.Object);
            services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
            services.EnableBingCapConsumerCancellationRetry(options =>
            {
                options.DrainTimeout = TimeSpan.FromMilliseconds(30);
            });
            services.AddSingleton(new CapStorageMarkerService("runtime-drain-timeout-test"));
            services.AddSingleton(new CapMessageQueueMakerService("runtime-drain-timeout-test"));
            var initializer = new Mock<IStorageInitializer>();
            initializer.Setup(value => value.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton(initializer.Object);
            services.RemoveAll<IProcessingServer>();
            services.AddSingleton<IProcessingServer>(gate);
        }).Build();

        try
        {
            var hosted = host.Services.GetServices<IHostedService>().Single();
            await hosted.StartAsync(CancellationToken.None);
            var failure = await Assert.ThrowsAsync<TimeoutException>(() => hosted.StopAsync(CancellationToken.None));

            Assert.True((bool)failure.Data["Bing.CapConsumerDrainIncomplete"]);
            runtime.Verify(value => value.BeginDrain(), Times.Once);
            runtime.Verify(value => value.WaitForDrainAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None), Times.Once);
            await Assert.ThrowsAsync<TimeoutException>(() => hosted.StopAsync(CancellationToken.None));
        }
        finally
        {
            Record.Exception(host.Dispose);
        }
    }

    /// <summary>
    /// 验证启动和清理同时失败时保留原始异常。
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenBootstrapAndCleanupFail_ShouldKeepBothErrorsAndShutdownOnce()
    {
        var original = new InvalidOperationException("bootstrap failed");
        var cleanup = new ApplicationException("cleanup failed");
        var bootstrapper = new Mock<IBootstrapper>();
        bootstrapper.Setup(value => value.BootstrapAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);
        bootstrapper.Setup(value => value.DisposeAsync()).Returns(() => ValueTask.FromException(cleanup));
        var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddCapEventBus(_ => { });
            services.Replace(ServiceDescriptor.Singleton(bootstrapper.Object));
        }).Build();
        var hosted = host.Services.GetServices<IHostedService>().Single();
        try
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => hosted.StartAsync(CancellationToken.None));
            Assert.Same(original, failure);
            Assert.Same(cleanup, failure.Data["Bing.CapStartupCleanupException"]);
            Assert.Same(cleanup, await Assert.ThrowsAsync<ApplicationException>(() => hosted.StopAsync(CancellationToken.None)));
            Assert.Same(cleanup, await Assert.ThrowsAsync<ApplicationException>(() => hosted.StopAsync(CancellationToken.None)));
        }
        finally
        {
            Assert.Same(cleanup, Record.Exception(host.Dispose));
        }
        bootstrapper.Verify(value => value.BootstrapAsync(CancellationToken.None), Times.Once);
        bootstrapper.Verify(value => value.DisposeAsync(), Times.Once);
    }

    /// <summary>
    /// 固定原生消费者在已取消启动令牌下的释放缺陷。
    /// </summary>
    /// <remarks>直接调用原生接口用于证明底层边界，不把此用法当作 Bing 宿主入口。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeConsumer_WhenStartedWithCancelledToken_ShouldExposeDisposedCancellationSource(bool cancelledBeforeStart)
    {
        using var cancellation = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
        services.AddSingleton(Mock.Of<IDataStorage>());
        services.AddSingleton(Mock.Of<IConsumerClientFactory>());
        services.AddSingleton(Mock.Of<ITransport>());
        var selector = new Mock<IConsumerServiceSelector>();
        selector.Setup(value => value.SelectCandidates())
            .Callback(() => cancellation.Cancel())
            .Returns(Array.Empty<ConsumerExecutorDescriptor>());
        services.AddSingleton(selector.Object);
        using var provider = services.BuildServiceProvider();
        var consumer = provider.GetRequiredService<IConsumerRegister>();
        if (cancelledBeforeStart)
            cancellation.Cancel();

        await consumer.Start(cancellation.Token);

        Assert.Throws<ObjectDisposedException>(() => consumer.Dispose());
        consumer.Dispose();
    }

    /// <summary>
    /// 创建包含真实 CAP 启动器的宿主。
    /// </summary>
    private static IHost CreateHost(ProcessorGate gate, bool aspectCore)
    {
        var builder = new HostBuilder();
        if (aspectCore)
            builder.UseServiceContext();
        return builder.ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddCapEventBus(options => options.UseDispatchingPerGroup = false);
            services.AddSingleton(new CapStorageMarkerService("lifecycle-test"));
            services.AddSingleton(new CapMessageQueueMakerService("lifecycle-test"));
            var initializer = new Mock<IStorageInitializer>();
            initializer.Setup(value => value.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton(initializer.Object);
            services.RemoveAll<IProcessingServer>();
            services.AddSingleton<IProcessingServer>(gate);
        }).Build();
    }

    /// <summary>
    /// 控制真实启动器调用处理器的时序。
    /// </summary>
    private sealed class ProcessorGate : IProcessingServer
    {
        /// <summary>
        /// 有效释放标识。
        /// </summary>
        private int _disposed;
        /// <summary>
        /// 有效启动次数。
        /// </summary>
        private int _starts;
        /// <summary>
        /// 有效释放次数。
        /// </summary>
        private int _disposals;
        /// <summary>
        /// 启动进入通知。
        /// </summary>
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 允许启动完成的通知。
        /// </summary>
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>
        /// 启动次数。
        /// </summary>
        public int Starts => Volatile.Read(ref _starts);
        /// <summary>
        /// 释放次数。
        /// </summary>
        public int Disposals => Volatile.Read(ref _disposals);
        /// <summary>
        /// 启动完成前的取消状态。
        /// </summary>
        public bool CancelledDuringStart { get; private set; }
        /// <summary>
        /// 释放时的可控回调。
        /// </summary>
        public Action OnDispose { get; set; }

        /// <inheritdoc />
        public async Task Start(CancellationToken stoppingToken)
        {
            Interlocked.Increment(ref _starts);
            Entered.TrySetResult(true);
            await Release.Task;
            CancelledDuringStart = stoppingToken.IsCancellationRequested;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Increment(ref _disposals);
                OnDispose?.Invoke();
            }
        }
    }
}
