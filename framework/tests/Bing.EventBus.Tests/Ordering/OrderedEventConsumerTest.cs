using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序消费者宿主注册、分页和停止行为。
/// </summary>
public sealed class OrderedEventConsumerTest
{
    /// <summary>
    /// 验证失败与缺号实体不阻止后续分页中的健康实体。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenBlockedStreamsFillFirstPage_ShouldProcessOthersInSeparateScopes()
    {
        using var database = new OrderingSqliteFixture();
        var store = database.CreateStore();
        var streams = Enumerable.Range(0, 12).Select(index => OrderingSqliteFixture.Stream($"sku-{index}"))
            .OrderBy(store.GetStreamKey, StringComparer.Ordinal).Take(4).ToArray();
        var failed = streams[0];
        var gap = streams[1];
        var healthy = streams.Skip(2).ToArray();
        await database.InTransactionAsync(async transaction =>
        {
            await store.EnqueueAsync(transaction, "stable-consumer", failed,
                new OrderedEventMessage("failed", 1, "failed"));
            await store.RecordFailureAsync(transaction, "stable-consumer", failed, 1, "HANDLER_ERROR");
            await store.EnqueueAsync(transaction, "stable-consumer", gap,
                new OrderedEventMessage("gap", 3, "gap"));
            for (var index = 0; index < healthy.Length; index++)
                await store.EnqueueAsync(transaction, "stable-consumer", healthy[index],
                    new OrderedEventMessage($"healthy-{index}", 1, healthy[index].EntityKey));
        });
        var observation = new HandlerObservation(expected: 2);
        using var host = CreateHost(database, observation, batchSize: 1);

        await host.StartAsync();
        try
        {
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitForCommittedBusinessAsync(database, 2, TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(new[] { "healthy-0", "healthy-1" },
            (await database.ReadBusinessIdsAsync()).OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(2, observation.HandlerIds.Distinct().Count());
        Assert.Equal(2, observation.EventIds.Length);
        Assert.DoesNotContain("failed", observation.EventIds);
        Assert.DoesNotContain("gap", observation.EventIds);
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), store);
        Assert.True((await coordinator.GetStateAsync("stable-consumer", failed)).Failed);
        Assert.Equal(0, (await coordinator.GetStateAsync("stable-consumer", gap)).LastSequence);
    }

    /// <summary>
    /// 验证失败流在等待人工重试期间不会重复执行，重试后仍可恢复处理。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenStreamFailed_ShouldBackoffUntilExplicitRetry()
    {
        using var database = new OrderingSqliteFixture();
        var inner = database.CreateStore();
        var stream = OrderingSqliteFixture.Stream("failed-backoff");
        await database.InTransactionAsync(async transaction =>
        {
            await inner.EnqueueAsync(transaction, "stable-consumer", stream,
                new OrderedEventMessage("failed-backoff", 1, stream.EntityKey));
            await inner.RecordFailureAsync(transaction, "stable-consumer", stream, 1, "HANDLER_ERROR");
        });

        var store = new CountingOrderedEventStore(inner);
        var observation = new HandlerObservation(expected: 1);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped(_ => database.CreateFactory());
                services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 1);
            })
            .Build();

        await host.StartAsync();
        try
        {
            await store.WaitForFirstProcessAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);
            Assert.Equal(1, store.ProcessCount);
            Assert.True(store.StateReadCount > 0);

            await database.InTransactionAsync(transaction =>
                inner.RetryAsync(transaction, "stable-consumer", stream));
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitForCommittedBusinessAsync(database, 1, TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(2, store.ProcessCount);
        Assert.Equal(new[] { "failed-backoff" }, await database.ReadBusinessIdsAsync());
    }

    /// <summary>
    /// 验证重复接收由真实存储去重且宿主停止后不再处理。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenEventIsDeliveredTwice_ShouldHandleOnceAndStop()
    {
        using var database = new OrderingSqliteFixture();
        var coordinator = new OrderedEventCoordinator(database.CreateFactory(), database.CreateStore());
        var stream = OrderingSqliteFixture.Stream();
        var message = new OrderedEventMessage("once", 1, "payload");
        await coordinator.ReceiveAsync("stable-consumer", stream, message);
        await coordinator.ReceiveAsync("stable-consumer", stream, message);
        var observation = new HandlerObservation(expected: 1);
        using var host = CreateHost(database, observation, batchSize: 1);

        await host.StartAsync();
        await observation.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitForCommittedBusinessAsync(database, 1, TimeSpan.FromSeconds(10));
        using (var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            await host.StopAsync(stopping.Token);

        Assert.Equal(new[] { "once" }, await database.ReadBusinessIdsAsync());
        Assert.Equal(new[] { "once" }, observation.EventIds);
        Assert.Equal(1, (await coordinator.GetStateAsync("stable-consumer", stream)).LastSequence);
    }

    /// <summary>
    /// 验证处理器构造失败后在途指标归零并可继续处理事件。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenHandlerConstructionFails_ShouldClearInFlightAndProcessNextEvent()
    {
        using var database = new OrderingSqliteFixture();
        var store = database.CreateStore();
        var stream = OrderingSqliteFixture.Stream("construction-failure");
        await database.InTransactionAsync(transaction => store.EnqueueAsync(transaction, "stable-consumer", stream,
            new OrderedEventMessage("construction-retry", 1, stream.EntityKey)));
        var construction = new HandlerConstructionFailure();
        var observation = new HandlerObservation(expected: 1);
        using var host = CreateHost<ConstructionFailingHandler>(database, observation, batchSize: 1,
            configure: services => services.AddSingleton(construction));
        var metrics = host.Services.GetRequiredService<IOrderedEventMetrics>();

        await host.StartAsync();
        try
        {
            await construction.WaitForFailureAsync(TimeSpan.FromSeconds(10));
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitForCommittedBusinessAsync(database, 1, TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(new[] { "construction-retry" }, await database.ReadBusinessIdsAsync());
        Assert.Equal(new[] { "construction-retry" }, observation.EventIds);
        Assert.Equal(0, metrics.Snapshot.InFlightCount);
    }

    /// <summary>
    /// 验证处理器持续构造失败时按退避间隔重试。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenHandlerConstructionKeepsFailing_ShouldBackoff()
    {
        using var database = new OrderingSqliteFixture();
        var store = database.CreateStore();
        var stream = OrderingSqliteFixture.Stream("persistent-construction-failure");
        await database.InTransactionAsync(transaction => store.EnqueueAsync(transaction, "stable-consumer", stream,
            new OrderedEventMessage("persistent-construction-failure", 1, stream.EntityKey)));
        var failure = new PersistentHandlerConstructionFailure();
        var observation = new HandlerObservation(expected: 1);
        using var host = CreateHost<PersistentConstructionFailingHandler>(database, observation, batchSize: 1,
            configure: services => services.AddSingleton(failure));

        await host.StartAsync();
        try
        {
            await failure.WaitForFailureAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);
            Assert.InRange(failure.Attempts, 1, 4);
            Assert.Empty(observation.EventIds);
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }
    }

    /// <summary>
    /// 验证并发失败登记不会突破失败探测缓存上限。
    /// </summary>
    [Fact]
    public void ConsumerWorker_WhenManyFailuresArriveConcurrently_ShouldBoundProbeCache()
    {
        using var database = new OrderingSqliteFixture();
        using var host = CreateHost(database, new HandlerObservation(expected: 1), batchSize: 1);
        var worker = host.Services.GetRequiredService<IOrderedEventConsumerRuntime>();
        var workerType = worker.GetType();
        var defer = workerType.GetMethod("DeferFailedStream", BindingFlags.Instance | BindingFlags.NonPublic);
        var cacheField = workerType.GetField("_failedStreamProbes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(defer);
        Assert.NotNull(cacheField);
        Parallel.For(0, 5000, index =>
            defer.Invoke(worker, new object[] { $"concurrent-failure-{index}" }));

        var cache = cacheField.GetValue(worker);
        var count = (int)cache.GetType().GetProperty("Count").GetValue(cache);
        Assert.InRange(count, 0, 4096);
    }

    /// <summary>
    /// 验证重复缺口只记录一次并限制缺口键缓存规模。
    /// </summary>
    [Fact]
    public void ConsumerWorker_WhenGapRepeats_ShouldThrottleAndBoundWarnings()
    {
        using var database = new OrderingSqliteFixture();
        using var host = CreateHost(database, new HandlerObservation(expected: 1), batchSize: 1);
        var worker = host.Services.GetRequiredService<IOrderedEventConsumerRuntime>();
        var workerType = worker.GetType();
        var shouldLog = workerType.GetMethod("ShouldLogGap", BindingFlags.Instance | BindingFlags.NonPublic);
        var clear = workerType.GetMethod("ClearGapWarning", BindingFlags.Instance | BindingFlags.NonPublic);
        var cacheField = workerType.GetField("_gapWarningUntil", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(shouldLog);
        Assert.NotNull(clear);
        Assert.NotNull(cacheField);
        Assert.True((bool)shouldLog.Invoke(worker, new object[] { "gap-stream" }));
        Assert.False((bool)shouldLog.Invoke(worker, new object[] { "gap-stream" }));
        clear.Invoke(worker, new object[] { "gap-stream" });
        Assert.True((bool)shouldLog.Invoke(worker, new object[] { "gap-stream" }));

        for (var index = 0; index < 5000; index++)
            shouldLog.Invoke(worker, new object[] { $"gap-stream-{index}" });

        var cache = cacheField.GetValue(worker);
        var count = (int)cache.GetType().GetProperty("Count").GetValue(cache);
        Assert.InRange(count, 0, 4096);
    }

    /// <summary>
    /// 验证失败缓存满载后先探测持久化状态，避免未知失败流重复开启业务事务。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenFailureProbeCacheIsFull_ShouldProbeStateBeforeRetryingBusiness()
    {
        using var database = new OrderingSqliteFixture();
        var observation = new HandlerObservation(expected: 1);
        using var host = CreateHost<AlwaysFailingHandler>(database, observation, batchSize: 1);
        var worker = host.Services.GetRequiredService<IOrderedEventConsumerRuntime>();
        var workerType = worker.GetType();
        var defer = workerType.GetMethod("DeferFailedStream", BindingFlags.Instance | BindingFlags.NonPublic);
        var cacheField = workerType.GetField("_failedStreamProbes", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(defer);
        Assert.NotNull(cacheField);
        var tryAdd = cacheField.FieldType.GetMethod("TryAdd");
        Assert.NotNull(tryAdd);
        var cache = cacheField.GetValue(worker);
        for (var index = 0; index < 4096; index++)
            Assert.True((bool)tryAdd.Invoke(cache, new object[] { $"filled-failure-{index}", long.MaxValue }));
        Assert.Equal(4096, (int)cache.GetType().GetProperty("Count").GetValue(cache));
        Assert.False((bool)defer.Invoke(worker, new object[] { "overflow-failure" }));

        var store = database.CreateStore();
        var stream = OrderingSqliteFixture.Stream("overflow-failure");
        await database.InTransactionAsync(transaction => store.EnqueueAsync(transaction, "stable-consumer", stream,
            new OrderedEventMessage("overflow-failure-event", 1, stream.EntityKey)));

        await host.StartAsync();
        try
        {
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(250);
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(1, observation.EventIds.Length);
    }

    /// <summary>
    /// 验证作用域释放失败后在途指标归零并可继续处理后续流。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenScopeDisposalFails_ShouldClearInFlightAndProcessNextStream()
    {
        using var database = new OrderingSqliteFixture();
        var store = database.CreateStore();
        var streams = new[]
        {
            OrderingSqliteFixture.Stream("dispose-failure-0"),
            OrderingSqliteFixture.Stream("dispose-failure-1")
        };
        await database.InTransactionAsync(async transaction =>
        {
            await store.EnqueueAsync(transaction, "stable-consumer", streams[0],
                new OrderedEventMessage("dispose-retry-0", 1, streams[0].EntityKey));
            await store.EnqueueAsync(transaction, "stable-consumer", streams[1],
                new OrderedEventMessage("dispose-retry-1", 1, streams[1].EntityKey));
        });
        var disposal = new ScopeDisposalFailure();
        var observation = new HandlerObservation(expected: 2);
        using var host = CreateHost<DisposalFailingHandler>(database, observation, batchSize: 1,
            configure: services =>
            {
                services.AddSingleton(disposal);
                services.AddScoped<ThrowingDisposalDependency>();
            });
        var metrics = host.Services.GetRequiredService<IOrderedEventMetrics>();

        await host.StartAsync();
        try
        {
            await disposal.WaitForFailureAsync(TimeSpan.FromSeconds(10));
            await observation.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitForCommittedBusinessAsync(database, 2, TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(new[] { "dispose-retry-0", "dispose-retry-1" },
            (await database.ReadBusinessIdsAsync()).OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(2, observation.EventIds.Length);
        Assert.Equal(0, metrics.Snapshot.InFlightCount);
    }

    /// <summary>
    /// 验证同一逻辑消费者不能重复注册。
    /// </summary>
    [Fact]
    public void AddBingOrderedConsumer_WhenSameConsumerRepeats_ShouldReject()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer");

        Assert.Throws<InvalidOperationException>(() =>
            services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer"));
        Assert.Single(services.Where(item => item.ServiceType == typeof(IHostedService)));
        Assert.Single(services, item => item.ServiceType == typeof(CapOrderedEventIngress));
    }

    /// <summary>
    /// 验证逻辑消费者名称拒绝首尾空白，避免数据库身份不一致。
    /// </summary>
    [Theory]
    [InlineData(" stable-consumer")]
    [InlineData("stable-consumer ")]
    public void AddBingOrderedConsumer_WhenConsumerHasBoundaryWhitespace_ShouldReject(string consumer)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddBingOrderedConsumer<RecordingHandler>(consumer));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IHostedService));
    }

    /// <summary>
    /// 验证有序消费者拒绝无效的流并发上限。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AddBingOrderedConsumer_WhenMaxConcurrencyIsInvalid_ShouldReject(int maxConcurrency)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer",
                maxConcurrency: maxConcurrency));
    }

    /// <summary>
    /// 验证有序消费者拒绝无效的连续扫描页数上限。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void AddBingOrderedConsumer_WhenMaxPagesPerScanIsInvalid_ShouldReject(int maxPagesPerScan)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer",
                maxPagesPerScan: maxPagesPerScan));
    }

    /// <summary>
    /// 验证后台消费者尚未启动时停止不会误报排空失败。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenStoppedBeforeStart_ShouldNotReportIncompleteDrain()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOrderedEventStore>(new ConcurrencyOrderedEventStore());
        services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
        services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer");
        await using var provider = services.BuildServiceProvider();

        var worker = provider.GetServices<IHostedService>().Single();
        await worker.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 验证不同业务流可以并行处理且不超过配置上限。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenMaxConcurrencyIsTwo_ShouldBoundDifferentStreams()
    {
        var store = new ConcurrencyOrderedEventStore();
        foreach (var index in Enumerable.Range(0, 4))
        {
            var stream = OrderingSqliteFixture.Stream($"concurrency-{index}");
            store.Add(stream, new OrderedEventMessage($"event-{index}", 1, $"payload-{index}"));
        }
        var observation = new ConcurrencyObservation(expected: 4);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<ConcurrentHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 4, maxConcurrency: 2);
            })
            .Build();

        await host.StartAsync();
        try
        {
            await observation.WaitForConcurrentAsync(TimeSpan.FromSeconds(10));
            observation.Release();
            await observation.WaitForCompletedAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(4, observation.Completed);
        Assert.Equal(2, observation.MaxActive);
    }

    /// <summary>
    /// 验证慢业务流释放并发槽位前，后续业务流可以继续处理。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenOneStreamIsSlow_ShouldRefillAvailableSlot()
    {
        var store = new ConcurrencyOrderedEventStore();
        store.Add(OrderingSqliteFixture.Stream("a-slow"), new OrderedEventMessage("slow", 1, "slow"));
        store.Add(OrderingSqliteFixture.Stream("b-fast"), new OrderedEventMessage("fast-1", 1, "fast-1"));
        store.Add(OrderingSqliteFixture.Stream("c-fast"), new OrderedEventMessage("fast-2", 1, "fast-2"));
        var observation = new SlowStreamObservation();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<SlowStreamHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 3, maxConcurrency: 2);
            })
            .Build();

        await host.StartAsync();
        try
        {
            await observation.WaitForSecondFastStreamAsync(TimeSpan.FromSeconds(10));
            Assert.False(observation.SlowReleased);
            observation.ReleaseSlowStream();
            await observation.WaitForCompletedAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.Equal(3, observation.Completed);
    }

    /// <summary>
    /// 验证连续满页扫描达到预算后会回绕，避免较小的新流长期等待。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenPagesRemainFull_ShouldWrapCursorForFairness()
    {
        var firstScan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ConcurrencyOrderedEventStore("1-blocked", firstScan);
        store.Add(OrderingSqliteFixture.Stream("1-blocked"),
            new OrderedEventMessage("blocked", 1, "blocked"));
        foreach (var index in Enumerable.Range(0, 20))
        {
            var key = $"2-high-{index:D2}";
            store.Add(OrderingSqliteFixture.Stream(key), new OrderedEventMessage(key, 1, key));
        }

        var observation = new FairnessObservation();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<FairnessHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(1), batchSize: 1, maxConcurrency: 1,
                    maxPagesPerScan: 2);
            })
            .Build();

        await host.StartAsync();
        try
        {
            using (var firstScanTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                await firstScan.Task.WaitAsync(firstScanTimeout.Token);
            store.Add(OrderingSqliteFixture.Stream("0-new"),
                new OrderedEventMessage("0-new", 1, "0-new"));
            await observation.WaitForNewStreamAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }

        Assert.InRange(observation.HighCompletedAtNewStream, 0, 2);
    }

    /// <summary>
    /// 验证处理器不响应取消时停止只报告排空未完成，不提前释放活动作用域。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenHandlerIgnoresCancellation_ShouldReportIncompleteDrainAndWaitLater()
    {
        var store = new ConcurrencyOrderedEventStore();
        var stream = OrderingSqliteFixture.Stream("uncooperative-stop");
        store.Add(stream, new OrderedEventMessage("uncooperative", 1, stream.EntityKey));
        var observation = new UncooperativeObservation();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<UncooperativeHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 1);
            })
            .Build();
        var metrics = host.Services.GetRequiredService<IOrderedEventMetrics>();
        var worker = host.Services.GetServices<IHostedService>().Single();

        await host.StartAsync();
        await observation.WaitForEnteredAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, metrics.Snapshot.InFlightCount);

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        {
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                worker.StopAsync(cancellation.Token));
            Assert.True(exception.Data.Contains("Bing.OrderedEventConsumerDrainIncomplete"));
            Assert.Equal(1L, exception.Data["Bing.OrderedEventConsumerInFlight"]);
        }
        Assert.Equal(1, metrics.Snapshot.InFlightCount);
        Assert.Equal(1L, await store.GetPendingStreamCountAsync(null, "stable-consumer"));

        observation.Release();
        await worker.StopAsync(CancellationToken.None);
        Assert.Equal(0, metrics.Snapshot.InFlightCount);
        Assert.Equal(1L, await store.GetPendingStreamCountAsync(null, "stable-consumer"));
        await host.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 验证运维排空只停止新扫描，并等待当前处理真实退出。
    /// </summary>
    [Fact]
    public async Task ConsumerRuntime_WhenBeginDrain_ShouldWaitForActiveHandlerWithoutForcingRelease()
    {
        var store = new ConcurrencyOrderedEventStore();
        var stream = OrderingSqliteFixture.Stream("runtime-drain");
        store.Add(stream, new OrderedEventMessage("runtime-drain", 1, stream.EntityKey));
        var observation = new UncooperativeObservation();
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<UncooperativeHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 1);
            })
            .Build();
        var runtime = host.Services.GetServices<IOrderedEventConsumerRuntime>().Single();

        await host.StartAsync();
        await observation.WaitForEnteredAsync(TimeSpan.FromSeconds(10));
        runtime.ConsumerName.ShouldBe("stable-consumer");
        runtime.IsRunning.ShouldBeTrue();
        runtime.InFlightCount.ShouldBe(1);

        runtime.BeginDrain();
        runtime.IsDraining.ShouldBeTrue();
        (await runtime.WaitForDrainAsync(TimeSpan.FromMilliseconds(20))).ShouldBeFalse();
        runtime.InFlightCount.ShouldBe(1);

        observation.Release();
        (await runtime.WaitForDrainAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        runtime.IsRunning.ShouldBeFalse();
        runtime.InFlightCount.ShouldBe(0);

        await host.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 验证消费者指标使用全局待处理流数量，不受分页大小影响。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenPendingStreamsExceedBatch_ShouldRecordGlobalCount()
    {
        var store = new ConcurrencyOrderedEventStore();
        foreach (var index in Enumerable.Range(0, 3))
        {
            var stream = OrderingSqliteFixture.Stream($"global-count-{index}");
            store.Add(stream, new OrderedEventMessage($"global-count-{index}", 1, $"value-{index}"));
        }
        var observation = new ConcurrencyObservation(expected: 2);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped<IOrderedEventTransactionFactory, ConcurrencyTransactionFactory>();
                services.AddBingOrderedConsumer<ConcurrentHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 2, maxConcurrency: 2);
            })
            .Build();
        var metrics = host.Services.GetRequiredService<IOrderedEventMetrics>();

        await host.StartAsync();
        try
        {
            await observation.WaitForConcurrentAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(3, metrics.Snapshot.PendingStreams);
            observation.Release();
            await observation.WaitForCompletedAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }
    }

    /// <summary>
    /// 扫描失败时保留旧快照并增加可观测的失败计数。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenPendingStreamScanFails_ShouldRecordScanFailure()
    {
        using var database = new OrderingSqliteFixture();
        var store = new FailingScanOrderedEventStore(database.CreateStore(), pauseAfterFailure: true);
        var observation = new HandlerObservation(expected: 1);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped(_ => database.CreateFactory());
                services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 1);
            })
            .Build();
        var metrics = host.Services.GetRequiredService<BingOrderedEventMetrics>();
        metrics.RecordPendingEvents(42);
        metrics.RecordOldestPendingEventAge(TimeSpan.FromSeconds(7));

        await host.StartAsync();
        try
        {
            await store.WaitForFailureAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (metrics.Snapshot.ScanFailureCount == 0)
                await Task.Delay(10, timeout.Token);

            metrics.Snapshot.ScanFailureCount.ShouldBeGreaterThan(0);
            metrics.Snapshot.PendingStreams.ShouldBe(0);
            metrics.Snapshot.PendingEvents.ShouldBe(42);
            metrics.Snapshot.OldestPendingEventAge.ShouldBe(TimeSpan.FromSeconds(7));
        }
        finally
        {
            store.ReleaseFailure();
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }
    }

    /// <summary>
    /// 验证全局积压和最老年龄统计失败时分别记录扫描失败。
    /// </summary>
    [Fact]
    public async Task HostedWorker_WhenStatisticsScansFail_ShouldRecordScanFailures()
    {
        using var database = new OrderingSqliteFixture();
        var store = new FailingScanOrderedEventStore(database.CreateStore(),
            failInitialScan: false, failPendingStatistics: true, failPendingAgeStatistics: true,
            pauseAfterFailure: true);
        var observation = new HandlerObservation(expected: 1);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                services.AddSingleton<IOrderedEventStore>(store);
                services.AddScoped(_ => database.CreateFactory());
                services.AddBingOrderedConsumer<RecordingHandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: 1);
            })
            .Build();
        var metrics = host.Services.GetRequiredService<BingOrderedEventMetrics>();
        metrics.RecordPendingEvents(42);
        metrics.RecordOldestPendingEventAge(TimeSpan.FromSeconds(7));

        await host.StartAsync();
        try
        {
            await store.WaitForFailureAsync(TimeSpan.FromSeconds(10));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (metrics.Snapshot.ScanFailureCount < 2)
                await Task.Delay(10, timeout.Token);

            metrics.Snapshot.ScanFailureCount.ShouldBeGreaterThanOrEqualTo(2);
            metrics.Snapshot.PendingStreams.ShouldBe(0);
            metrics.Snapshot.PendingEvents.ShouldBe(42);
            metrics.Snapshot.OldestPendingEventAge.ShouldBe(TimeSpan.FromSeconds(7));
        }
        finally
        {
            store.ReleaseFailure();
            using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await host.StopAsync(stopping.Token);
        }
    }

    /// <summary>
    /// 创建真实 HostedService 与按作用域注册的事务依赖。
    /// </summary>
    private static IHost CreateHost(OrderingSqliteFixture database, HandlerObservation observation, int batchSize) =>
        CreateHost<RecordingHandler>(database, observation, batchSize);

    /// <summary>
    /// 创建指定处理器的真实后台消费者宿主。
    /// </summary>
    private static IHost CreateHost<THandler>(OrderingSqliteFixture database, HandlerObservation observation,
        int batchSize, Action<IServiceCollection> configure = null)
        where THandler : class, IOrderedEventHandler =>
        new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(observation);
                configure?.Invoke(services);
                services.AddSingleton<IOrderedEventStore>(database.CreateStore());
                services.AddScoped(_ => database.CreateFactory());
                services.AddBingOrderedConsumer<THandler>("stable-consumer",
                    pollInterval: TimeSpan.FromMilliseconds(10), batchSize: batchSize);
            })
            .Build();

    /// <summary>
    /// 等待业务事务实际提交。
    /// </summary>
    private static async Task WaitForCommittedBusinessAsync(OrderingSqliteFixture database, int expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await database.ReadBusinessIdsAsync()).Length == expected)
                    return;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 6)
            {
                // SQLite 共享缓存的写事务结束后再次读取。
            }
            await Task.Delay(20);
        }
        Assert.Equal(expected, (await database.ReadBusinessIdsAsync()).Length);
    }

    /// <summary>
    /// 记录每次业务处理所属的作用域。
    /// </summary>
    private sealed class RecordingHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 处理观察器。
        /// </summary>
        private readonly HandlerObservation _observation;

        /// <summary>
        /// 本作用域实例标识。
        /// </summary>
        private readonly Guid _handlerId = Guid.NewGuid();

        /// <summary>
        /// 初始化一个 <see cref="RecordingHandler" /> 类型的实例。
        /// </summary>
        public RecordingHandler(HandlerObservation observation) => _observation = observation;

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, cancellationToken);
            _observation.Record(_handlerId, message.EventId);
        }
    }

    /// <summary>
    /// 首次构造处理器时注入一次性故障。
    /// </summary>
    private sealed class HandlerConstructionFailure
    {
        /// <summary>
        /// 剩余故障次数。
        /// </summary>
        private int _remainingFailures = 1;

        /// <summary>
        /// 首次构造失败信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _failed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 通知并按配置抛出构造故障。
        /// </summary>
        internal void ThrowIfConfigured()
        {
            if (Interlocked.Decrement(ref _remainingFailures) < 0)
                return;
            _failed.TrySetResult(true);
            throw new InvalidOperationException("测试处理器构造失败。");
        }

        /// <summary>
        /// 等待首次构造失败。
        /// </summary>
        internal async Task WaitForFailureAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _failed.Task.WaitAsync(cancellation.Token);
        }
    }

    /// <summary>
    /// 首次解析时构造失败、后续可正常处理的有序事件处理器。
    /// </summary>
    private sealed class ConstructionFailingHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 处理观察器。
        /// </summary>
        private readonly HandlerObservation _observation;

        /// <summary>
        /// 本作用域实例标识。
        /// </summary>
        private readonly Guid _handlerId = Guid.NewGuid();

        /// <summary>
        /// 初始化一个 <see cref="ConstructionFailingHandler" /> 类型的实例。
        /// </summary>
        public ConstructionFailingHandler(HandlerObservation observation, HandlerConstructionFailure failure)
        {
            failure.ThrowIfConfigured();
            _observation = observation;
        }

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, cancellationToken);
            _observation.Record(_handlerId, message.EventId);
        }
    }

    /// <summary>
    /// 记录持续的处理器构造失败。
    /// </summary>
    private sealed class PersistentHandlerConstructionFailure
    {
        /// <summary>
        /// 构造尝试次数。
        /// </summary>
        private int _attempts;

        /// <summary>
        /// 首次失败信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _failed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取构造尝试次数。
        /// </summary>
        internal int Attempts => Volatile.Read(ref _attempts);

        /// <summary>
        /// 抛出持续构造故障。
        /// </summary>
        internal void Throw()
        {
            Interlocked.Increment(ref _attempts);
            _failed.TrySetResult(true);
            throw new InvalidOperationException("测试处理器持续构造失败。");
        }

        /// <summary>
        /// 等待首次构造失败。
        /// </summary>
        /// <param name="timeout">等待超时时间。</param>
        internal async Task WaitForFailureAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _failed.Task.WaitAsync(cancellation.Token);
        }
    }

    /// <summary>
    /// 始终在构造阶段失败的有序事件处理器。
    /// </summary>
    private sealed class PersistentConstructionFailingHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 初始化一个 <see cref="PersistentConstructionFailingHandler" /> 类型的实例。
        /// </summary>
        public PersistentConstructionFailingHandler(PersistentHandlerConstructionFailure failure) =>
            failure.Throw();

        /// <inheritdoc />
        public Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>
    /// 记录业务调用次数并持续抛出故障的有序事件处理器。
    /// </summary>
    private sealed class AlwaysFailingHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 业务处理观察器。
        /// </summary>
        private readonly HandlerObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="AlwaysFailingHandler" /> 类型的实例。
        /// </summary>
        public AlwaysFailingHandler(HandlerObservation observation) => _observation = observation;

        /// <inheritdoc />
        public Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            _observation.Record(Guid.Empty, message.EventId);
            throw new InvalidOperationException("测试业务持续失败。");
        }
    }

    /// <summary>
    /// 首次作用域释放时注入一次性故障。
    /// </summary>
    private sealed class ScopeDisposalFailure
    {
        /// <summary>
        /// 剩余故障次数。
        /// </summary>
        private int _remainingFailures = 1;

        /// <summary>
        /// 首次释放失败信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _failed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 按配置执行依赖释放并注入故障。
        /// </summary>
        internal ValueTask DisposeAsync()
        {
            if (Interlocked.Decrement(ref _remainingFailures) < 0)
                return ValueTask.CompletedTask;
            _failed.TrySetResult(true);
            return ValueTask.FromException(new InvalidOperationException("测试作用域释放失败。"));
        }

        /// <summary>
        /// 等待首次作用域释放失败。
        /// </summary>
        internal async Task WaitForFailureAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _failed.Task.WaitAsync(cancellation.Token);
        }
    }

    /// <summary>
    /// 在作用域释放阶段向测试状态注入故障的依赖。
    /// </summary>
    private sealed class ThrowingDisposalDependency : IAsyncDisposable
    {
        /// <summary>
        /// 释放故障控制器。
        /// </summary>
        private readonly ScopeDisposalFailure _failure;

        /// <summary>
        /// 初始化一个 <see cref="ThrowingDisposalDependency" /> 类型的实例。
        /// </summary>
        public ThrowingDisposalDependency(ScopeDisposalFailure failure) => _failure = failure;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => _failure.DisposeAsync();
    }

    /// <summary>
    /// 依赖可异步释放故障资源的有序事件处理器。
    /// </summary>
    private sealed class DisposalFailingHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 处理观察器。
        /// </summary>
        private readonly HandlerObservation _observation;

        /// <summary>
        /// 保持释放故障依赖存活到作用域结束。
        /// </summary>
        private readonly ThrowingDisposalDependency _dependency;

        /// <summary>
        /// 本作用域实例标识。
        /// </summary>
        private readonly Guid _handlerId = Guid.NewGuid();

        /// <summary>
        /// 初始化一个 <see cref="DisposalFailingHandler" /> 类型的实例。
        /// </summary>
        public DisposalFailingHandler(HandlerObservation observation, ThrowingDisposalDependency dependency)
        {
            _observation = observation;
            _dependency = dependency;
        }

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            _ = _dependency;
            await OrderingSqliteFixture.WriteBusinessAsync(message, transaction, cancellationToken);
            _observation.Record(_handlerId, message.EventId);
        }
    }

    /// <summary>
    /// 仅用于并发上限测试的处理器。
    /// </summary>
    private sealed class ConcurrentHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 并发观察器。
        /// </summary>
        private readonly ConcurrencyObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="ConcurrentHandler" /> 类型的实例。
        /// </summary>
        public ConcurrentHandler(ConcurrencyObservation observation) => _observation = observation;

        /// <inheritdoc />
        public Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken) =>
            _observation.WaitUntilReleasedAsync(cancellationToken);
    }

    /// <summary>
    /// 控制慢流和快速流处理进度的观察器。
    /// </summary>
    private sealed class SlowStreamObservation
    {
        /// <summary>
        /// 慢流进入处理的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _slowEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 第二个快速流进入处理的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _secondFastStream =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 释放慢流的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _releaseSlow =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 全部流完成的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _allCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 已进入处理的快速流数量。
        /// </summary>
        private int _fastEntered;

        /// <summary>
        /// 已完成的流数量。
        /// </summary>
        private int _completed;

        /// <summary>
        /// 获取慢流是否已经释放。
        /// </summary>
        internal bool SlowReleased => _releaseSlow.Task.IsCompleted;

        /// <summary>
        /// 获取已完成的流数量。
        /// </summary>
        internal int Completed => Volatile.Read(ref _completed);

        /// <summary>
        /// 标记慢流已经进入处理。
        /// </summary>
        internal void EnterSlowStream() => _slowEntered.TrySetResult(true);

        /// <summary>
        /// 等待慢流进入处理。
        /// </summary>
        internal async Task WaitForSlowStreamAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _slowEntered.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 标记快速流已经进入处理。
        /// </summary>
        internal void EnterFastStream()
        {
            if (Interlocked.Increment(ref _fastEntered) == 2)
                _secondFastStream.TrySetResult(true);
        }

        /// <summary>
        /// 等待第二个快速流进入处理。
        /// </summary>
        internal async Task WaitForSecondFastStreamAsync(TimeSpan timeout)
        {
            await WaitForSlowStreamAsync(timeout);
            using var cancellation = new CancellationTokenSource(timeout);
            await _secondFastStream.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 释放慢流继续处理。
        /// </summary>
        internal void ReleaseSlowStream() => _releaseSlow.TrySetResult(true);

        /// <summary>
        /// 等待慢流释放信号。
        /// </summary>
        internal Task WaitForSlowReleaseAsync() => _releaseSlow.Task;

        /// <summary>
        /// 记录一个流完成。
        /// </summary>
        internal void RecordCompleted()
        {
            if (Interlocked.Increment(ref _completed) == 3)
                _allCompleted.TrySetResult(true);
        }

        /// <summary>
        /// 等待全部流完成。
        /// </summary>
        internal async Task WaitForCompletedAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _allCompleted.Task.WaitAsync(cancellation.Token);
        }
    }

    /// <summary>
    /// 用于验证慢流隔离的处理器。
    /// </summary>
    private sealed class SlowStreamHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 处理观察器。
        /// </summary>
        private readonly SlowStreamObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="SlowStreamHandler" /> 类型的实例。
        /// </summary>
        public SlowStreamHandler(SlowStreamObservation observation) => _observation = observation;

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            if (message.EventId == "slow")
            {
                _observation.EnterSlowStream();
                await _observation.WaitForSlowReleaseAsync();
            }
            else
                _observation.EnterFastStream();

            _observation.RecordCompleted();
        }
    }

    /// <summary>
    /// 记录跨页公平调度测试状态。
    /// </summary>
    private sealed class FairnessObservation
    {
        /// <summary>
        /// 新流已经被处理的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _newStream =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 已完成的旧流数量。
        /// </summary>
        private int _highCompleted;

        /// <summary>
        /// 新流处理时已完成的旧流数量。
        /// </summary>
        internal int HighCompletedAtNewStream { get; private set; }

        /// <summary>
        /// 记录一个旧流完成。
        /// </summary>
        internal void RecordHighStream() => Interlocked.Increment(ref _highCompleted);

        /// <summary>
        /// 记录新流开始处理。
        /// </summary>
        internal void RecordNewStream()
        {
            HighCompletedAtNewStream = Volatile.Read(ref _highCompleted);
            _newStream.TrySetResult(true);
        }

        /// <summary>
        /// 等待新流完成处理。
        /// </summary>
        internal async Task WaitForNewStreamAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _newStream.Task.WaitAsync(cancellation.Token);
        }
    }

    /// <summary>
    /// 为跨页公平调度测试提供有界处理延迟。
    /// </summary>
    private sealed class FairnessHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 公平调度观察器。
        /// </summary>
        private readonly FairnessObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="FairnessHandler" /> 类型的实例。
        /// </summary>
        /// <param name="observation">公平调度观察器。</param>
        public FairnessHandler(FairnessObservation observation) => _observation = observation;

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            if (message.EventId == "0-new")
            {
                _observation.RecordNewStream();
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            _observation.RecordHighStream();
        }
    }

    /// <summary>
    /// 记录不会响应取消的处理器状态。
    /// </summary>
    private sealed class UncooperativeObservation
    {
        /// <summary>
        /// 处理器已经进入的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 允许处理器结束的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 标记处理器已进入。
        /// </summary>
        internal void Enter() => _entered.TrySetResult(true);

        /// <summary>
        /// 等待处理器进入。
        /// </summary>
        internal async Task WaitForEnteredAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _entered.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 释放不会响应取消的处理器。
        /// </summary>
        internal void Release() => _release.TrySetResult(true);

        /// <summary>
        /// 等待释放信号。
        /// </summary>
        internal Task WaitForReleaseAsync() => _release.Task;
    }

    /// <summary>
    /// 在释放信号前忽略宿主取消的有序事件处理器。
    /// </summary>
    private sealed class UncooperativeHandler : IOrderedEventHandler
    {
        /// <summary>
        /// 处理状态观察器。
        /// </summary>
        private readonly UncooperativeObservation _observation;

        /// <summary>
        /// 初始化一个 <see cref="UncooperativeHandler" /> 类型的实例。
        /// </summary>
        public UncooperativeHandler(UncooperativeObservation observation) => _observation = observation;

        /// <inheritdoc />
        public async Task HandleAsync(OrderedEventStream stream, OrderedEventMessage message,
            DbTransaction transaction, CancellationToken cancellationToken)
        {
            _observation.Enter();
            await _observation.WaitForReleaseAsync();
        }
    }

    /// <summary>
    /// 记录处理器并发度并控制测试释放时机。
    /// </summary>
    private sealed class ConcurrencyObservation
    {
        /// <summary>
        /// 期望完成次数。
        /// </summary>
        private readonly int _expected;

        /// <summary>
        /// 当前活动处理数。
        /// </summary>
        private int _active;

        /// <summary>
        /// 最大活动处理数。
        /// </summary>
        private int _maxActive;

        /// <summary>
        /// 已完成处理数。
        /// </summary>
        private int _completed;

        /// <summary>
        /// 首次达到并发阈值的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _concurrent =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 允许处理器继续执行的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 达到期望完成数的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _finished =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 初始化一个 <see cref="ConcurrencyObservation" /> 类型的实例。
        /// </summary>
        /// <param name="expected">期望完成次数。</param>
        public ConcurrencyObservation(int expected) => _expected = expected;

        /// <summary>
        /// 获取最大活动处理数。
        /// </summary>
        public int MaxActive => Volatile.Read(ref _maxActive);

        /// <summary>
        /// 获取已完成处理数。
        /// </summary>
        public int Completed => Volatile.Read(ref _completed);

        /// <summary>
        /// 等待处理器达到并发阈值。
        /// </summary>
        public async Task WaitForConcurrentAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _concurrent.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 释放等待中的处理器。
        /// </summary>
        public void Release() => _release.TrySetResult(true);

        /// <summary>
        /// 等待所有事件完成。
        /// </summary>
        public async Task WaitForCompletedAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _finished.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 等待测试释放并记录一次处理。
        /// </summary>
        public async Task WaitUntilReleasedAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            while (active > Volatile.Read(ref _maxActive) &&
                   Interlocked.CompareExchange(ref _maxActive, active, Volatile.Read(ref _maxActive)) !=
                   Volatile.Read(ref _maxActive))
            {
                active = Volatile.Read(ref _active);
            }
            if (active >= 2)
                _concurrent.TrySetResult(true);
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                if (Interlocked.Increment(ref _completed) >= _expected)
                    _finished.TrySetResult(true);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    /// <summary>
    /// 为并发上限测试提供无数据库事务的有序存储。
    /// </summary>
    private sealed class ConcurrencyOrderedEventStore : IOrderedEventStore, IOrderedEventStorePendingStreamStatistics
    {
        /// <summary>
        /// 待处理事件。
        /// </summary>
        private readonly object _sync = new();

        /// <summary>
        /// 待处理业务流集合。
        /// </summary>
        private readonly Dictionary<string, (OrderedEventStream Stream, OrderedEventMessage Message)> _pending = new();

        /// <summary>
        /// 需要保持阻塞状态的流键。
        /// </summary>
        private readonly string _blockedEntityKey;

        /// <summary>
        /// 首次扫描完成信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _firstScan;

        /// <summary>
        /// 已执行的主扫描次数。
        /// </summary>
        private int _scanCount;

        /// <summary>
        /// 初始化一个 <see cref="ConcurrencyOrderedEventStore" /> 类型的实例。
        /// </summary>
        /// <param name="blockedEntityKey">保持缺口状态的流键。</param>
        /// <param name="firstScan">首次主扫描完成信号。</param>
        public ConcurrencyOrderedEventStore(string blockedEntityKey = null,
            TaskCompletionSource<bool> firstScan = null)
        {
            _blockedEntityKey = blockedEntityKey;
            _firstScan = firstScan;
        }

        /// <summary>
        /// 添加待处理事件。
        /// </summary>
        public void Add(OrderedEventStream stream, OrderedEventMessage message)
        {
            lock (_sync)
                _pending[GetStreamKey(stream)] = (stream, message);
        }

        /// <inheritdoc />
        public Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            OrderedEventMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public async Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
        {
            (OrderedEventStream Stream, OrderedEventMessage Message) item;
            lock (_sync)
            {
                if (!_pending.TryGetValue(GetStreamKey(stream), out item))
                    return OrderedEventProcessingResult.Empty;
            }
            if (string.Equals(stream.EntityKey, _blockedEntityKey, StringComparison.Ordinal))
                return OrderedEventProcessingResult.Gap;
            await handler(item.Message, transaction, cancellationToken);
            lock (_sync)
                _pending.Remove(GetStreamKey(stream));
            return OrderedEventProcessingResult.Processed;
        }

        /// <inheritdoc />
        public Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            long failedSequence, string errorCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction,
            string consumer, int limit, string afterStreamKey = null, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var streams = _pending.Values
                    .Where(item => string.IsNullOrEmpty(afterStreamKey) ||
                                   string.CompareOrdinal(GetStreamKey(item.Stream), afterStreamKey) > 0)
                    .OrderBy(item => GetStreamKey(item.Stream), StringComparer.Ordinal)
                    .Take(limit)
                    .Select(item => item.Stream)
                    .ToArray();
                if (Interlocked.Increment(ref _scanCount) == 1)
                    _firstScan?.TrySetResult(true);
                return Task.FromResult<IReadOnlyList<OrderedEventStream>>(streams);
            }
        }

        /// <inheritdoc />
        public Task<long> GetPendingStreamCountAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
                return Task.FromResult((long)_pending.Count);
        }

        /// <inheritdoc />
        public string GetStreamKey(OrderedEventStream stream) =>
            $"{stream.TenantId}:{stream.Stream}:{stream.EntityKey}";
    }

    /// <summary>
    /// 记录失败流探测和处理次数的有序事件存储包装器。
    /// </summary>
    private sealed class CountingOrderedEventStore : IOrderedEventStore,
        IOrderedEventStorePendingStreamStatistics, IOrderedEventStoreStatistics,
        IOrderedEventStorePendingAgeStatistics
    {
        /// <summary>
        /// 实际有序事件存储。
        /// </summary>
        private readonly IOrderedEventStore _inner;

        /// <summary>
        /// 首次处理调用信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _firstProcess =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 处理调用次数。
        /// </summary>
        private int _processCount;

        /// <summary>
        /// 状态读取次数。
        /// </summary>
        private int _stateReadCount;

        /// <summary>
        /// 初始化一个 <see cref="CountingOrderedEventStore" /> 类型的实例。
        /// </summary>
        /// <param name="inner">实际有序事件存储。</param>
        internal CountingOrderedEventStore(IOrderedEventStore inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        /// <summary>
        /// 获取处理调用次数。
        /// </summary>
        internal int ProcessCount => Volatile.Read(ref _processCount);

        /// <summary>
        /// 获取状态读取次数。
        /// </summary>
        internal int StateReadCount => Volatile.Read(ref _stateReadCount);

        /// <summary>
        /// 等待首次处理调用。
        /// </summary>
        /// <param name="timeout">等待超时时间。</param>
        internal async Task WaitForFirstProcessAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _firstProcess.Task.WaitAsync(cancellation.Token);
        }

        /// <inheritdoc />
        public Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
            CancellationToken cancellationToken = default) =>
            _inner.AllocateSequenceAsync(transaction, stream, cancellationToken);

        /// <inheritdoc />
        public Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            OrderedEventMessage message, CancellationToken cancellationToken = default) =>
            _inner.EnqueueAsync(transaction, consumer, stream, message, cancellationToken);

        /// <inheritdoc />
        public async Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction,
            string consumer, OrderedEventStream stream,
            Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _processCount);
            _firstProcess.TrySetResult(true);
            return await _inner.ProcessNextAsync(transaction, consumer, stream, handler, cancellationToken);
        }

        /// <inheritdoc />
        public Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            long failedSequence, string errorCode, CancellationToken cancellationToken = default) =>
            _inner.RecordFailureAsync(transaction, consumer, stream, failedSequence, errorCode, cancellationToken);

        /// <inheritdoc />
        public Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) =>
            _inner.RetryAsync(transaction, consumer, stream, cancellationToken);

        /// <inheritdoc />
        public async Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _stateReadCount);
            return await _inner.GetStateAsync(transaction, consumer, stream, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction,
            string consumer, int limit, string afterStreamKey = null, CancellationToken cancellationToken = default) =>
            _inner.GetPendingStreamsAsync(transaction, consumer, limit, afterStreamKey, cancellationToken);

        /// <inheritdoc />
        public Task<long> GetPendingStreamCountAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default) =>
            ((IOrderedEventStorePendingStreamStatistics)_inner).GetPendingStreamCountAsync(transaction, consumer,
                cancellationToken);

        /// <inheritdoc />
        public Task<long> GetPendingEventCountAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default) =>
            ((IOrderedEventStoreStatistics)_inner).GetPendingEventCountAsync(transaction, consumer, cancellationToken);

        /// <inheritdoc />
        public Task<DateTime?> GetOldestPendingEventUtcAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default) =>
            ((IOrderedEventStorePendingAgeStatistics)_inner).GetOldestPendingEventUtcAsync(transaction, consumer,
                cancellationToken);

        /// <inheritdoc />
        public string GetStreamKey(OrderedEventStream stream) => _inner.GetStreamKey(stream);
    }

    /// <summary>
    /// 首次分页扫描失败、后续调用转发到真实存储的包装器。
    /// </summary>
    private sealed class FailingScanOrderedEventStore : IOrderedEventStore,
        IOrderedEventStoreStatistics, IOrderedEventStorePendingAgeStatistics
    {
        /// <summary>
        /// 实际有序事件存储。
        /// </summary>
        private readonly IOrderedEventStore _inner;

        /// <summary>
        /// 首次扫描失败信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _failed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 剩余扫描故障次数。
        /// </summary>
        private int _remainingFailures = 1;

        /// <summary>
        /// 是否注入全局积压统计故障。
        /// </summary>
        private readonly bool _failPendingStatistics;

        /// <summary>
        /// 是否已经注入过一次扫描或统计故障。
        /// </summary>
        private int _failureObserved;

        /// <summary>
        /// 是否注入最老积压年龄统计故障。
        /// </summary>
        private readonly bool _failPendingAgeStatistics;

        /// <summary>
        /// 是否在首次主扫描失败后暂停重试。
        /// </summary>
        private readonly bool _pauseAfterFailure;

        /// <summary>
        /// 允许首次主扫描失败继续重试的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _resumeAfterFailure =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 全局积压统计剩余故障次数。
        /// </summary>
        private int _remainingPendingStatisticsFailures;

        /// <summary>
        /// 最老积压年龄统计剩余故障次数。
        /// </summary>
        private int _remainingPendingAgeStatisticsFailures;

        /// <summary>
        /// 初始化一个 <see cref="FailingScanOrderedEventStore" /> 类型的实例。
        /// </summary>
        /// <param name="inner">实际有序事件存储。</param>
        /// <param name="failInitialScan">是否注入首次主扫描故障。</param>
        /// <param name="failPendingStatistics">是否注入全局积压统计故障。</param>
        /// <param name="failPendingAgeStatistics">是否注入最老积压年龄统计故障。</param>
        /// <param name="pauseAfterFailure">是否在首次主扫描失败后暂停重试。</param>
        public FailingScanOrderedEventStore(IOrderedEventStore inner,
            bool failInitialScan = true, bool failPendingStatistics = false, bool failPendingAgeStatistics = false,
            bool pauseAfterFailure = false)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _failPendingStatistics = failPendingStatistics;
            _failPendingAgeStatistics = failPendingAgeStatistics;
            _pauseAfterFailure = pauseAfterFailure;
            _remainingFailures = failInitialScan ? 1 : 0;
            _remainingPendingStatisticsFailures = failPendingStatistics ? 1 : 0;
            _remainingPendingAgeStatisticsFailures = failPendingAgeStatistics ? 1 : 0;
        }

        /// <summary>
        /// 等待首次扫描失败。
        /// </summary>
        public async Task WaitForFailureAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _failed.Task.WaitAsync(cancellation.Token);
        }

        /// <summary>
        /// 允许首次主扫描失败继续执行后续重试。
        /// </summary>
        public void ReleaseFailure() => _resumeAfterFailure.TrySetResult(true);

        /// <inheritdoc />
        public Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
            CancellationToken cancellationToken = default) =>
            _inner.AllocateSequenceAsync(transaction, stream, cancellationToken);

        /// <inheritdoc />
        public Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            OrderedEventMessage message, CancellationToken cancellationToken = default) =>
            _inner.EnqueueAsync(transaction, consumer, stream, message, cancellationToken);

        /// <inheritdoc />
        public Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default) =>
            _inner.ProcessNextAsync(transaction, consumer, stream, handler, cancellationToken);

        /// <inheritdoc />
        public Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            long failedSequence, string errorCode, CancellationToken cancellationToken = default) =>
            _inner.RecordFailureAsync(transaction, consumer, stream, failedSequence, errorCode, cancellationToken);

        /// <inheritdoc />
        public Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) =>
            _inner.RetryAsync(transaction, consumer, stream, cancellationToken);

        /// <inheritdoc />
        public Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, CancellationToken cancellationToken = default) =>
            _inner.GetStateAsync(transaction, consumer, stream, cancellationToken);

        /// <inheritdoc />
        public Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction,
            string consumer, int limit, string afterStreamKey = null,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _remainingFailures, 0) == 1)
            {
                _failed.TrySetResult(true);
                Interlocked.Exchange(ref _failureObserved, 1);
                return Task.FromException<IReadOnlyList<OrderedEventStream>>(
                    new InvalidOperationException("测试扫描失败。"));
            }

            if (_pauseAfterFailure && Volatile.Read(ref _failureObserved) == 1)
                return WaitBeforeRetryAsync(transaction, consumer, limit, afterStreamKey, cancellationToken);

            return _inner.GetPendingStreamsAsync(transaction, consumer, limit, afterStreamKey, cancellationToken);
        }

        /// <summary>
        /// 在测试需要时暂停已记录故障后的下一次主扫描。
        /// </summary>
        private async Task<IReadOnlyList<OrderedEventStream>> WaitBeforeRetryAsync(DbTransaction transaction,
            string consumer, int limit, string afterStreamKey, CancellationToken cancellationToken)
        {
            await _resumeAfterFailure.Task;
            return await _inner.GetPendingStreamsAsync(transaction, consumer, limit, afterStreamKey,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<long> GetPendingEventCountAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default)
        {
            if (_failPendingStatistics && Interlocked.Exchange(ref _remainingPendingStatisticsFailures, 0) == 1)
            {
                _failed.TrySetResult(true);
                Interlocked.Exchange(ref _failureObserved, 1);
                return Task.FromException<long>(new InvalidOperationException("测试积压统计失败。"));
            }

            return ((IOrderedEventStoreStatistics)_inner).GetPendingEventCountAsync(transaction, consumer,
                cancellationToken);
        }

        /// <inheritdoc />
        public Task<DateTime?> GetOldestPendingEventUtcAsync(DbTransaction transaction, string consumer,
            CancellationToken cancellationToken = default)
        {
            if (_failPendingAgeStatistics &&
                Interlocked.Exchange(ref _remainingPendingAgeStatisticsFailures, 0) == 1)
            {
                _failed.TrySetResult(true);
                Interlocked.Exchange(ref _failureObserved, 1);
                return Task.FromException<DateTime?>(new InvalidOperationException("测试最老积压统计失败。"));
            }

            return ((IOrderedEventStorePendingAgeStatistics)_inner).GetOldestPendingEventUtcAsync(transaction,
                consumer, cancellationToken);
        }

        /// <inheritdoc />
        public string GetStreamKey(OrderedEventStream stream) => _inner.GetStreamKey(stream);
    }

    /// <summary>
    /// 提供保存点支持的内存有序事务。
    /// </summary>
    private sealed class ConcurrencyTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <inheritdoc />
        public Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IOrderedEventTransaction>(new ConcurrencyTransaction());
    }

    /// <summary>
    /// 并发上限测试使用的空事务。
    /// </summary>
    private sealed class ConcurrencyTransaction : IOrderedEventTransaction
    {
        /// <summary>
        /// 空数据库事务。
        /// </summary>
        private readonly DbTransaction _transaction = new ConcurrencyDbTransaction();

        /// <inheritdoc />
        public DbTransaction Transaction => _transaction;

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// 支持保存点调用的空数据库事务。
    /// </summary>
    private sealed class ConcurrencyDbTransaction : DbTransaction
    {
        /// <inheritdoc />
        public override bool SupportsSavepoints => true;

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        protected override DbConnection DbConnection => null;

        /// <inheritdoc />
        public override void Commit()
        {
        }

        /// <inheritdoc />
        public override void Rollback()
        {
        }

        /// <inheritdoc />
        public override void Save(string savepointName)
        {
        }

        /// <inheritdoc />
        public override void Rollback(string savepointName)
        {
        }

        /// <inheritdoc />
        public override void Release(string savepointName)
        {
        }
    }

    /// <summary>
    /// 线程安全地观察后台处理结果。
    /// </summary>
    private sealed class HandlerObservation
    {
        /// <summary>
        /// 处理标识。
        /// </summary>
        private readonly ConcurrentQueue<Guid> _handlerIds = new();

        /// <summary>
        /// 事件标识。
        /// </summary>
        private readonly ConcurrentQueue<string> _eventIds = new();

        /// <summary>
        /// 期望处理次数。
        /// </summary>
        private readonly int _expected;

        /// <summary>
        /// 达到期望次数的信号。
        /// </summary>
        private readonly TaskCompletionSource<bool> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 初始化一个 <see cref="HandlerObservation" /> 类型的实例。
        /// </summary>
        public HandlerObservation(int expected) => _expected = expected;

        /// <summary>
        /// 获取处理器实例标识。
        /// </summary>
        public Guid[] HandlerIds => _handlerIds.ToArray();

        /// <summary>
        /// 获取事件标识。
        /// </summary>
        public string[] EventIds => _eventIds.ToArray();

        /// <summary>
        /// 记录一次业务处理。
        /// </summary>
        public void Record(Guid handlerId, string eventId)
        {
            _handlerIds.Enqueue(handlerId);
            _eventIds.Enqueue(eventId);
            if (_eventIds.Count >= _expected)
                _completed.TrySetResult(true);
        }

        /// <summary>
        /// 等待期望业务处理次数。
        /// </summary>
        public async Task WaitAsync(TimeSpan timeout)
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await _completed.Task.WaitAsync(cancellation.Token);
        }
    }
}
