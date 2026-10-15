using Microsoft.Extensions.Hosting;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>
/// 直接验证取消夹具在停止、回调排空和资源释放边界上的清理职责。
/// </summary>
public sealed class AdminCapCancellationCleanupTest
{
    /// <summary>
    /// 实际停止任务超时时保留数据和资源，且延迟停止完成不补做清理。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenStopWaitTimesOut_ShouldRetainDataAndResources()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new TestHost(stopTask: release.Task);
        var orm = new TestHost();
        var observation = CreateObservation();
        var dataCalls = 0;
        var cleanup = new CapCancellationCleanup(host, orm, observation,
            () => { dataCalls++; return Task.CompletedTask; }, null, TimeSpan.FromMilliseconds(30));
        Exception failure;
        try
        {
            failure = await cleanup.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<TimeoutException>(failure);
            Assert.Contains(observation.EventId, Assert.IsType<string>(failure.Data["Bing.TestCapCancellationRecovery"]));
            Assert.Equal(CancellationToken.None, host.StopToken);
            Assert.Equal(0, dataCalls);
            Assert.Equal(0, host.DisposeCalls);
            Assert.Equal(0, orm.DisposeCalls);
        }
        finally
        {
            release.TrySetResult(true);
        }
        Assert.Same(failure, await cleanup.RunAsync());
        Assert.Equal(1, host.StopCalls);
        Assert.Equal(0, dataCalls);
        Assert.Equal(0, host.DisposeCalls);
        Assert.Equal(0, orm.DisposeCalls);
    }

    /// <summary>
    /// 停止直接抛错时保留原始验证异常，且不删除数据或释放依赖。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenStopThrows_ShouldPreserveOriginalAndRetainResources()
    {
        var original = new InvalidOperationException("原始验证失败。");
        var stopFailure = new ApplicationException("实际停止失败。");
        var host = new TestHost(stopFailure: stopFailure);
        var orm = new TestHost();
        var dataCalls = 0;
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(),
            () => { dataCalls++; return Task.CompletedTask; }, original, TimeSpan.FromSeconds(5));

        Assert.Same(original, await cleanup.RunAsync());
        Assert.Same(stopFailure, original.Data["Bing.TestCapCancellationHostStopException"]);
        Assert.Equal(0, dataCalls);
        Assert.Equal(0, host.DisposeCalls);
        Assert.Equal(0, orm.DisposeCalls);
    }

    /// <summary>
    /// 宿主停止完成仍须等待已入场回调退出，并原子拒绝后来回调。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenCallbackIsActive_ShouldWaitForExitAndCloseAdmission()
    {
        var host = new TestHost();
        var observation = CreateObservation();
        Assert.True(observation.TryEnterCallback(out _));
        var dataCalls = 0;
        var cleanup = new CapCancellationCleanup(host, null, observation,
            () => { dataCalls++; return Task.CompletedTask; }, null, TimeSpan.FromSeconds(5));
        var task = cleanup.RunAsync();
        try
        {
            await host.StopEntered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(task.IsCompleted);
            Assert.False(observation.TryEnterCallback(out _));
            var subscriber = new AdminMySqlCapIntegrationTest.CapCancellationSubscriber(observation);
            await Assert.ThrowsAsync<OperationCanceledException>(() => subscriber.ConsumeAsync(
                new AdminMySqlCapIntegrationTest.CapCancellationMessage { EventId = observation.EventId }, null));
            Assert.Equal(0, dataCalls);
            Assert.Equal(0, host.DisposeCalls);
        }
        finally
        {
            observation.ExitCallback();
        }
        Assert.Null(await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, dataCalls);
        Assert.Equal(1, host.DisposeCalls);
        Assert.Equal(observation.Started, observation.Ended);
    }

    /// <summary>
    /// 已停止宿主的活动业务回调排空超时时仍保留数据和根容器。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenCallbackDrainTimesOut_ShouldRetainDataAndResources()
    {
        var host = new TestHost();
        var observation = CreateObservation();
        Assert.True(observation.TryEnterCallback(out _));
        var dataCalls = 0;
        var cleanup = new CapCancellationCleanup(host, null, observation,
            () => { dataCalls++; return Task.CompletedTask; }, null, TimeSpan.FromMilliseconds(30));
        try
        {
            var failure = await cleanup.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<TimeoutException>(failure);
            Assert.Contains("StopConfirmed=True; CallbacksDrained=False",
                Assert.IsType<string>(failure.Data["Bing.TestCapCancellationRecovery"]));
            Assert.Equal(0, dataCalls);
            Assert.Equal(0, host.DisposeCalls);
        }
        finally
        {
            observation.ExitCallback();
        }
        await cleanup.RunAsync();
        Assert.Equal(0, dataCalls);
        Assert.Equal(0, host.DisposeCalls);
    }

    /// <summary>
    /// 重复或并发请求共享同一任务，正常仅执行一次异步释放路径。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenResourcesSupportAsyncDispose_ShouldShareTaskAndDisposeOnce()
    {
        var host = new AsyncTestHost();
        var orm = new AsyncTestHost();
        var dataCalls = 0;
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(),
            () => { dataCalls++; return Task.CompletedTask; }, null, TimeSpan.FromSeconds(5));

        var first = cleanup.RunAsync();
        var second = cleanup.RunAsync();
        Assert.Same(first, second);
        Assert.Null(await first);
        Assert.Equal(1, dataCalls);
        Assert.Equal(1, host.StopCalls);
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(1, orm.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
        Assert.Equal(0, orm.DisposeCalls);
    }

    /// <summary>
    /// 资源只支持同步释放时正常仅选择一次同步路径。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenResourcesSupportOnlySyncDispose_ShouldDisposeOnce()
    {
        var host = new TestHost();
        var orm = new TestHost();
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(), null, null,
            TimeSpan.FromSeconds(5));

        var first = cleanup.RunAsync();
        Assert.Same(first, cleanup.RunAsync());
        Assert.Null(await first);
        Assert.Equal(1, host.DisposeCalls);
        Assert.Equal(1, orm.DisposeCalls);
    }

    /// <summary>
    /// 安全排空后的数据清理异常不替换原始错误，仍释放已安全停止的资源。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenDataCleanupFails_ShouldPreserveOriginalAndDisposeSafeResources()
    {
        var original = new InvalidOperationException("原始业务验证错误。");
        var dataFailure = new ApplicationException("独占数据清理失败。");
        var host = new AsyncTestHost();
        var orm = new TestHost();
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(),
            () => Task.FromException(dataFailure), original, TimeSpan.FromSeconds(5));

        Assert.Same(original, await cleanup.RunAsync());
        Assert.Same(dataFailure, original.Data["Bing.TestCapCancellationDataCleanupException"]);
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
        Assert.Equal(1, orm.DisposeCalls);
    }

    /// <summary>
    /// 创建无需连接外部服务的独占事件观察状态。
    /// </summary>
    /// <returns>只用于夹具生命周期测试的观察状态。</returns>
    private static AdminMySqlCapIntegrationTest.CapCancellationObservation CreateObservation() =>
        new(Guid.NewGuid().ToString("N"), false);

    /// <summary>
    /// 宿主释放已失败完成时仍尝试释放 ORM，且保留原始异常。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenHostDisposeCompletesWithFailure_ShouldStillDisposeOrm()
    {
        var original = new InvalidOperationException("原始验证异常。");
        var disposeFailure = new ApplicationException("宿主释放失败。");
        var host = new AsyncTestHost(Task.FromException(disposeFailure));
        var orm = new TestHost();
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(), null, original,
            TimeSpan.FromSeconds(5));

        Assert.Same(original, await cleanup.RunAsync());
        Assert.Same(disposeFailure, original.Data["Bing.TestCapCancellationHostDisposeException"]);
        Assert.False(original.Data.Contains("Bing.TestCapCancellationPendingCleanupOwner"));
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, host.DisposeCalls);
        Assert.Equal(1, orm.DisposeCalls);
    }

    /// <summary>
    /// 宿主实际释放仍在等待时不得释放 ORM，且重复等待不会重试释放。
    /// </summary>
    [Fact]
    public async Task Cleanup_WhenHostDisposeIsStillPending_ShouldRetainOrmAndOwner()
    {
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new AsyncTestHost(release.Task);
        var orm = new TestHost();
        var cleanup = new CapCancellationCleanup(host, orm, CreateObservation(), null, null,
            TimeSpan.FromMilliseconds(30));
        try
        {
            var failure = await cleanup.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<TimeoutException>(failure);
            Assert.Same(cleanup, failure.Data["Bing.TestCapCancellationPendingCleanupOwner"]);
            Assert.Equal(1, host.AsyncDisposeCalls);
            Assert.Equal(0, host.DisposeCalls);
            Assert.Equal(0, orm.DisposeCalls);
        }
        finally
        {
            release.TrySetResult(true);
        }
        await cleanup.RunAsync();
        Assert.Equal(1, host.AsyncDisposeCalls);
        Assert.Equal(0, orm.DisposeCalls);
    }

    /// <summary>
    /// 提供可延迟停止、可抛错停止和同步释放计数的最小宿主。
    /// </summary>
    private class TestHost : IHost
    {
        /// <summary>
        /// 实际停止前需要等待的可选任务。
        /// </summary>
        private readonly Task _stopTask;

        /// <summary>
        /// 实际停止时直接抛出的可选异常。
        /// </summary>
        private readonly Exception _stopFailure;

        /// <summary>
        /// 实际停止调用已进入的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _stopEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 初始化一个 <see cref="TestHost" /> 类型的实例。
        /// </summary>
        /// <param name="stopTask">停止的实际任务；为空时同步完成。</param>
        /// <param name="stopFailure">停止直接抛出的异常；为空时不抛错。</param>
        internal TestHost(Task stopTask = null, Exception stopFailure = null)
        {
            _stopTask = stopTask;
            _stopFailure = stopFailure;
        }

        /// <inheritdoc />
        public IServiceProvider Services => throw new NotSupportedException("职责测试不解析服务。");

        /// <summary>
        /// 获取实际停止调用次数。
        /// </summary>
        internal int StopCalls { get; private set; }

        /// <summary>
        /// 获取实际停止收到的取消令牌。
        /// </summary>
        internal CancellationToken StopToken { get; private set; }

        /// <summary>
        /// 获取同步释放调用次数。
        /// </summary>
        internal int DisposeCalls { get; private set; }

        /// <summary>
        /// 获取实际停止已进入的等待任务。
        /// </summary>
        internal Task StopEntered => _stopEntered.Task;

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            StopToken = cancellationToken;
            _stopEntered.TrySetResult(true);
            if (_stopFailure != null)
                throw _stopFailure;
            return _stopTask ?? Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose() => DisposeCalls++;
    }

    /// <summary>
    /// 同时支持两种释放接口以验证唯一异步释放选择的最小宿主。
    /// </summary>
    private sealed class AsyncTestHost : TestHost, IAsyncDisposable
    {
        /// <summary>
        /// 异步释放返回的实际任务。
        /// </summary>
        private readonly Task _disposeTask;

        /// <summary>
        /// 初始化一个 <see cref="AsyncTestHost" /> 类型的实例。
        /// </summary>
        /// <param name="disposeTask">实际释放任务；为空时同步完成。</param>
        internal AsyncTestHost(Task disposeTask = null) => _disposeTask = disposeTask;

        /// <summary>
        /// 获取异步释放调用次数。
        /// </summary>
        internal int AsyncDisposeCalls { get; private set; }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCalls++;
            return new ValueTask(_disposeTask ?? Task.CompletedTask);
        }
    }
}
