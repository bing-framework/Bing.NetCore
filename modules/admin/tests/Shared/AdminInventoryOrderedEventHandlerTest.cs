using System.Data;
using System.Data.Common;
using Bing.Admin.Data.Inventory;
using Bing.Admin.Service.Events;
using Bing.Data.Events.Ordering;
using Bing.Data.Inventory;
using Xunit;

namespace Bing.Admin.InventoryTests;

/// <summary>
/// 验证 Admin 库存有序事件处理器的业务边界。
/// </summary>
public sealed class AdminInventoryOrderedEventHandlerTest
{
    /// <summary>
    /// 匹配的库存完成事件应把相同事务和稳定消费者传给投影存储。
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenEventMatchesStream_ShouldSaveUsingSameTransaction()
    {
        var store = new RecordingProjectionStore();
        var handler = new AdminInventoryOrderedEventHandler(store);
        var stream = new OrderedEventStream("tenant-a", AdminInventoryService.OrderedStream, "order-1");
        var message = new OrderedEventMessage("event-1", 3,
            "{\"TenantId\":\"tenant-a\",\"WarehouseId\":\"warehouse-1\",\"OrderId\":\"order-1\",\"Status\":\"Applied\"}",
            "v1");
        using var transaction = new TestDbTransaction();
        using var cancellation = new CancellationTokenSource();

        await handler.HandleAsync(stream, message, transaction, cancellation.Token);

        Assert.Same(transaction, store.Transaction);
        Assert.Equal(AdminInventoryService.OrderedConsumer, store.Consumer);
        Assert.Same(stream, store.Stream);
        Assert.Same(message, store.Message);
        Assert.Equal("Applied", store.Status);
        Assert.Equal(message.Payload, store.Payload);
        Assert.Equal(cancellation.Token, store.CancellationToken);
    }

    /// <summary>
    /// 业务流或租户不匹配时应拒绝写入投影。
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenStreamIdentityDoesNotMatch_ShouldRejectBeforeSaving()
    {
        var store = new RecordingProjectionStore();
        var handler = new AdminInventoryOrderedEventHandler(store);
        var stream = new OrderedEventStream("tenant-a", AdminInventoryService.OrderedStream, "order-1");
        var message = new OrderedEventMessage("event-1", 1,
            "{\"TenantId\":\"tenant-b\",\"WarehouseId\":\"warehouse-1\",\"OrderId\":\"order-1\",\"Status\":\"Applied\"}",
            "v1");
        using var transaction = new TestDbTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(stream, message, transaction, CancellationToken.None));

        Assert.Null(store.Transaction);
    }

    /// <summary>
    /// 不支持的状态或契约版本应在访问投影存储前失败。
    /// </summary>
    [Theory]
    [InlineData("v2", "Applied")]
    [InlineData("v1", "Unknown")]
    public async Task HandleAsync_WhenContractOrStatusIsInvalid_ShouldRejectBeforeSaving(string version,
        string status)
    {
        var store = new RecordingProjectionStore();
        var handler = new AdminInventoryOrderedEventHandler(store);
        var stream = new OrderedEventStream("tenant-a", AdminInventoryService.OrderedStream, "order-1");
        var message = new OrderedEventMessage("event-1", 1,
            $"{{\"TenantId\":\"tenant-a\",\"WarehouseId\":\"warehouse-1\",\"OrderId\":\"order-1\",\"Status\":\"{status}\"}}",
            version);
        using var transaction = new TestDbTransaction();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            handler.HandleAsync(stream, message, transaction, CancellationToken.None));

        Assert.Null(store.Transaction);
    }

    /// <summary>
    /// 超过投影上限的正文应在 JSON 解析前拒绝。
    /// </summary>
    [Fact]
    public async Task HandleAsync_WhenPayloadExceedsLimit_ShouldRejectBeforeParsingOrSaving()
    {
        var store = new RecordingProjectionStore();
        var handler = new AdminInventoryOrderedEventHandler(store);
        var stream = new OrderedEventStream("tenant-a", AdminInventoryService.OrderedStream, "order-1");
        var payload = "{\"TenantId\":\"tenant-a\",\"WarehouseId\":\"warehouse-1\",\"OrderId\":\"order-1\",\"Status\":\"Applied\",\"Padding\":\"" +
            new string('x', 65536) + "\"}";
        var message = new OrderedEventMessage("event-large", 1, payload, "v1");
        using var transaction = new TestDbTransaction();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(stream, message, transaction, CancellationToken.None));

        Assert.Null(store.Transaction);
    }

    /// <summary>
    /// 记录处理器传入投影存储的参数。
    /// </summary>
    private sealed class RecordingProjectionStore : IInventoryOrderProjectionStore
    {
        /// <summary>
        /// 保存的数据库事务。
        /// </summary>
        public DbTransaction Transaction { get; private set; }
        /// <summary>
        /// 保存的消费者名称。
        /// </summary>
        public string Consumer { get; private set; }
        /// <summary>
        /// 保存的业务流。
        /// </summary>
        public OrderedEventStream Stream { get; private set; }
        /// <summary>
        /// 保存的事件。
        /// </summary>
        public OrderedEventMessage Message { get; private set; }
        /// <summary>
        /// 保存的状态。
        /// </summary>
        public string Status { get; private set; }
        /// <summary>
        /// 保存的正文。
        /// </summary>
        public string Payload { get; private set; }
        /// <summary>
        /// 保存的取消令牌。
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <inheritdoc />
        public Task SaveAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
            OrderedEventMessage message, string status, string payload,
            CancellationToken cancellationToken = default)
        {
            Transaction = transaction;
            Consumer = consumer;
            Stream = stream;
            Message = message;
            Status = status;
            Payload = payload;
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 提供不执行提交操作的测试事务。
    /// </summary>
    private sealed class TestDbTransaction : DbTransaction
    {
        /// <inheritdoc />
        protected override DbConnection DbConnection => null;

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        public override void Commit()
        {
        }

        /// <inheritdoc />
        public override void Rollback()
        {
        }
    }
}
