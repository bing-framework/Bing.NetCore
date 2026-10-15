using System.Data.Common;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Bing.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件留存清理调度的资源与多消费者边界。
/// </summary>
public sealed class OrderedEventRetentionHostedServiceTest
{
    /// <summary>
    /// 调度默认关闭时不应要求协调器或执行清理。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenDisabled_ShouldNotRun()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options => options.Enabled = false);

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// 启用立即清理时应按配置处理多个稳定消费者。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenEnabled_ShouldDeleteProcessedRecordsForAllConsumers()
    {
        using var database = new OrderingSqliteFixture();
        await SeedProcessedEventAsync(database, "consumer-a", "event-a");
        await SeedProcessedEventAsync(database, "consumer-b", "event-b");
        await database.SetInboxCreatedAtAsync(DateTime.UtcNow.AddHours(-2));

        var services = CreateDatabaseServices(database);
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.RetentionPeriod = TimeSpan.FromHours(1);
            options.BatchSize = 10;
            options.Consumers.Add("consumer-a");
            options.Consumers.Add("consumer-b");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () => await database.CountInboxAsync() == 0);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(0, await database.CountInboxAsync());
    }

    /// <summary>
    /// 显式配置归档留存时应清理过期历史记录。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenArchiveRetentionIsConfigured_ShouldDeleteArchivedRecords()
    {
        using var database = new OrderingSqliteFixture();
        await SeedProcessedEventAsync(database, "consumer", "archived-event");
        await database.SetArchiveArchivedAtAsync(DateTime.UtcNow.AddHours(-2));

        var services = CreateDatabaseServices(database);
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.ArchiveRetentionPeriod = TimeSpan.FromHours(1);
            options.BatchSize = 10;
            options.ArchiveConsumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () => await database.CountArchiveAsync() == 0);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(0, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 仅配置归档清理时不应要求收件箱保留期限。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenArchiveOnly_ShouldIgnoreProcessedRetentionPeriod()
    {
        using var database = new OrderingSqliteFixture();
        await SeedProcessedEventAsync(database, "consumer", "archive-only-event");
        await database.SetArchiveArchivedAtAsync(DateTime.UtcNow.AddHours(-2));

        var services = CreateDatabaseServices(database);
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.RetentionPeriod = TimeSpan.Zero;
            options.ArchiveRetentionPeriod = TimeSpan.FromHours(1);
            options.BatchSize = 10;
            options.ArchiveConsumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await WaitUntilAsync(async () => await database.CountArchiveAsync() == 0);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(0, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 未获取到协调租约时应跳过本轮而不删除记录。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationLeaseUnavailable_ShouldSkipBatch()
    {
        using var database = new OrderingSqliteFixture();
        await SeedProcessedEventAsync(database, "consumer", "event");
        await database.SetInboxCreatedAtAsync(DateTime.UtcNow.AddHours(-2));

        var services = CreateDatabaseServices(database);
        services.AddScoped<ILockLeaseProvider, NullLeaseProvider>();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.RetentionPeriod = TimeSpan.FromHours(1);
            options.UseCoordinationLease = true;
            options.Consumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(1, await database.CountInboxAsync());
    }

    /// <summary>
    /// 启用协调租约却未注册提供程序时应在启动阶段拒绝配置。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinationProviderMissing_ShouldRejectStartup()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.UseCoordinationLease = true;
            options.Consumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        await Assert.ThrowsAsync<InvalidOperationException>(() => hosted.StartAsync(CancellationToken.None));
    }

    /// <summary>
    /// 启用调度却未注册协调器时应在启动阶段拒绝配置。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCoordinatorMissing_ShouldRejectStartup()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.Consumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hosted.StartAsync(CancellationToken.None));
        Assert.Contains(nameof(OrderedEventCoordinator), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 清理事务不响应取消时，停止应等待真实事务结束。
    /// </summary>
    [Fact]
    public async Task Scheduler_WhenCurrentCleanupIgnoresCancellation_ShouldWaitForCompletion()
    {
        var store = new BlockingRetentionStore();
        var services = new ServiceCollection();
        services.AddSingleton<IOrderedEventStore>(store);
        services.AddScoped<IOrderedEventTransactionFactory, BlockingTransactionFactory>();
        services.AddScoped<OrderedEventCoordinator>();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.RunImmediately = true;
            options.Interval = TimeSpan.FromHours(1);
            options.Consumers.Add("consumer");
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().Single(item =>
            item is OrderedEventRetentionHostedService);
        await hosted.StartAsync(CancellationToken.None);
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var stopCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var stopTask = hosted.StopAsync(stopCancellation.Token);
        await Task.Delay(100);
        Assert.False(stopTask.IsCompleted);

        store.Release.TrySetResult(true);
        await stopTask;
        Assert.Equal(1, store.CallCount);
    }

    /// <summary>
    /// 启用调度却没有消费者时应在构造托管服务时拒绝配置。
    /// </summary>
    [Fact]
    public void Scheduler_WhenNoConsumerIsConfigured_ShouldRejectConfiguration()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options => options.Enabled = true);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>()
            .Single(item => item is OrderedEventRetentionHostedService));
    }

    /// <summary>
    /// 归档清理缺少配套期限或消费者时应拒绝配置。
    /// </summary>
    [Fact]
    public void Scheduler_WhenArchiveConfigurationIsIncomplete_ShouldRejectConfiguration()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.ArchiveConsumers.Add("consumer");
        });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>()
            .Single(item => item is OrderedEventRetentionHostedService));
    }

    /// <summary>
    /// 归档清理缺少消费者但配置期限时应拒绝配置。
    /// </summary>
    [Fact]
    public void Scheduler_WhenArchiveConsumersAreMissing_ShouldRejectConfiguration()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventRetentionScheduler(options =>
        {
            options.Enabled = true;
            options.ArchiveRetentionPeriod = TimeSpan.FromDays(1);
        });

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetServices<IHostedService>()
            .Single(item => item is OrderedEventRetentionHostedService));
    }

    /// <summary>
    /// 创建使用测试数据库的协调器注册。
    /// </summary>
    private static IServiceCollection CreateDatabaseServices(OrderingSqliteFixture database)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOrderedEventStore>(database.CreateStore());
        services.AddScoped<IOrderedEventTransactionFactory>(_ => database.CreateFactory());
        services.AddScoped<OrderedEventCoordinator>();
        return services;
    }

    /// <summary>
    /// 写入并完成一个可被留存清理的收件记录。
    /// </summary>
    private static async Task SeedProcessedEventAsync(OrderingSqliteFixture database, string consumer,
        string eventId)
    {
        await using var provider = CreateDatabaseServices(database).BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<OrderedEventCoordinator>();
        var stream = OrderingSqliteFixture.Stream(consumer);
        await coordinator.ReceiveAsync(consumer, stream,
            new OrderedEventMessage(eventId, 1, eventId));
        Assert.Equal(OrderedEventProcessingResult.Processed,
            await coordinator.ProcessNextAsync(consumer, stream,
                (message, transaction, cancellationToken) => Task.CompletedTask));
    }

    /// <summary>
    /// 等待后台清理完成且不建立无限等待。
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(25);
        }
        Assert.True(await condition(), "后台留存清理未在测试预算内完成。" );
    }

    /// <summary>
    /// 返回空协调租约的测试实现。
    /// </summary>
    private sealed class NullLeaseProvider : ILockLeaseProvider
    {
        /// <inheritdoc />
        public Task<ILockLease> TryAcquireLeaseAsync(string key, TimeSpan waitTimeout,
            TimeSpan? leaseDuration = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ILockLease>(null);
        }
    }

    /// <summary>
    /// 等待外部释放的留存存储测试实现。
    /// </summary>
    private sealed class BlockingRetentionStore : IOrderedEventStore, IOrderedEventStoreRetention
    {
        /// <summary>
        /// 标识清理调用已经开始。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 允许清理调用结束。
        /// </summary>
        internal readonly TaskCompletionSource<bool> Release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取清理调用次数。
        /// </summary>
        internal int CallCount { get; private set; }

        /// <inheritdoc />
        public Task<int> DeleteProcessedBeforeAsync(DbTransaction transaction, string consumer,
            DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Started.TrySetResult(true);
            return WaitAsync();
        }

        /// <summary>
        /// 忽略取消并等待测试释放。
        /// </summary>
        private async Task<int> WaitAsync()
        {
            await Release.Task.ConfigureAwait(false);
            return 1;
        }

        /// <inheritdoc />
        public Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            OrderedEventMessage message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            long failedSequence, string errorCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
            OrderedEventStream stream, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction,
            string consumer, int limit, string afterStreamKey = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public string GetStreamKey(OrderedEventStream stream) => throw new NotSupportedException();
    }

    /// <summary>
    /// 返回阻塞留存事务的测试事务工厂。
    /// </summary>
    private sealed class BlockingTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <inheritdoc />
        public Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IOrderedEventTransaction>(new BlockingTransaction());
    }

    /// <summary>
    /// 不拥有真实连接、仅用于验证协调器停止等待的测试事务。
    /// </summary>
    private sealed class BlockingTransaction : IOrderedEventTransaction
    {
        /// <inheritdoc />
        public DbTransaction Transaction => null;

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
