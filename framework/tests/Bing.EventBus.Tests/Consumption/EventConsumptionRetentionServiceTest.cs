using System.Data.Common;
using Bing.Events.Consumption;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EventBus.Tests.Consumption;

/// <summary>
/// 验证事件消费收据高层授权服务。
/// </summary>
public sealed class EventConsumptionRetentionServiceTest
{
    /// <summary>
    /// 验证服务先授权再调用低层存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_ShouldAuthorizeBeforeStore()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var calls = new List<string>();
        var store = new RecordingStore(calls);
        var authorizer = new RecordingAuthorizer(calls);
        var service = new EventConsumptionRetentionService(store, authorizer);

        var result = await service.DeleteBeforeAsync(transaction, "inventory", DateTime.UtcNow.AddHours(-1), 20);

        Assert.Equal(7, result);
        Assert.Equal(new[] { "authorize", "store" }, calls);
        Assert.Equal("inventory", authorizer.Consumer);
        Assert.Equal(20, authorizer.BatchSize);
    }

    /// <summary>
    /// 验证授权失败时不会调用低层存储。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenAuthorizationFails_ShouldNotCallStore()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var store = new RecordingStore(new List<string>());
        var authorizer = new DenyingAuthorizer();
        var service = new EventConsumptionRetentionService(store, authorizer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteBeforeAsync(transaction,
            "inventory", DateTime.UtcNow.AddHours(-1), 20));

        Assert.Equal(0, store.CallCount);
    }

    /// <summary>
    /// 验证取消和非法参数在授权前拒绝。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotCallDependencies()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var calls = new List<string>();
        var store = new RecordingStore(calls);
        var authorizer = new RecordingAuthorizer(calls);
        var service = new EventConsumptionRetentionService(store, authorizer);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteBeforeAsync(transaction,
            "inventory", DateTime.UtcNow.AddHours(-1), 20, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteBeforeAsync(transaction,
            "inventory", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), 20));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.DeleteBeforeAsync(transaction,
            "inventory", DateTime.UtcNow.AddHours(-1), 0));

        Assert.Empty(calls);
    }

    /// <summary>
    /// 验证重复注册不会覆盖调用方已有授权器。
    /// </summary>
    [Fact]
    public void AddBingEventConsumptionRetention_ShouldKeepExistingAuthorizer()
    {
        var services = new ServiceCollection();
        var existing = new RecordingAuthorizer(new List<string>());
        services.AddSingleton<IEventConsumptionRetentionAuthorizer>(existing);
        services.AddSingleton<IEventConsumptionRetentionStore>(new RecordingStore(new List<string>()));
        services.AddBingEventConsumptionRetention<DenyingAuthorizer>();
        services.AddBingEventConsumptionRetention<RecordingAuthorizer>();

        using var provider = services.BuildServiceProvider();
        Assert.Same(existing, provider.GetRequiredService<IEventConsumptionRetentionAuthorizer>());
        Assert.IsType<EventConsumptionRetentionService>(
            provider.GetRequiredService<IEventConsumptionRetentionService>());
    }

    /// <summary>
    /// 记录授权和存储调用顺序。
    /// </summary>
    private sealed class RecordingAuthorizer : IEventConsumptionRetentionAuthorizer
    {
        /// <summary>
        /// 记录测试授权器的调用顺序。
        /// </summary>
        private readonly IList<string> _calls;

        /// <summary>
        /// 初始化一个 <see cref="RecordingAuthorizer" /> 类型的实例。
        /// </summary>
        public RecordingAuthorizer(IList<string> calls)
        {
            _calls = calls;
        }

        /// <summary>
        /// 最近一次消费者。
        /// </summary>
        public string Consumer { get; private set; }

        /// <summary>
        /// 最近一次批量。
        /// </summary>
        public int BatchSize { get; private set; }

        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default)
        {
            Consumer = consumer;
            BatchSize = batchSize;
            _calls.Add("authorize");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 拒绝清理的测试授权器。
    /// </summary>
    private sealed class DenyingAuthorizer : IEventConsumptionRetentionAuthorizer
    {
        /// <inheritdoc />
        public Task AuthorizeAsync(string consumer, DateTime cutoffUtc, int batchSize,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new UnauthorizedAccessException("测试授权拒绝。"));
    }

    /// <summary>
    /// 记录低层清理调用的测试存储。
    /// </summary>
    private sealed class RecordingStore : IEventConsumptionRetentionStore
    {
        /// <summary>
        /// 记录测试存储的调用顺序。
        /// </summary>
        private readonly IList<string> _calls;

        /// <summary>
        /// 初始化一个 <see cref="RecordingStore" /> 类型的实例。
        /// </summary>
        public RecordingStore(IList<string> calls)
        {
            _calls = calls;
        }

        /// <summary>
        /// 低层存储调用次数。
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc />
        public Task<int> DeleteBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
            int batchSize, CancellationToken cancellationToken = default)
        {
            CallCount++;
            _calls.Add("store");
            return Task.FromResult(7);
        }
    }
}
