using System.Data.Common;
using System.Text;
using Bing.Data.Inventory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证整单库存身份、首次结果及事务完成回调。
/// </summary>
public sealed class InventoryOrderStoreTest
{
    /// <summary>
    /// 验证独立结果查询不登记请求或执行事务回调。
    /// </summary>
    [Fact]
    public async Task FindAsync_ShouldReadCommittedFirstResultWithoutNewWrites()
    {
        using var database = new Database();
        Assert.Null(await database.FindAsync(Request("order", 3)));
        Assert.Equal(0, await database.CountAsync("bing_inventory_order"));
        await database.ExecuteAsync(Request("order", 3));
        var found = await database.FindAsync(Request("order", 3));
        Assert.True(found.Duplicate);
        Assert.Equal(new long?[] { 7 }, found.RemainingQuantities);
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.FindAsync(Request("order", 4)));
        Assert.Equal(1, await database.CountAsync("audit_test"));
        Assert.Equal(1, await database.CountAsync("outbox_test"));
    }

    /// <summary>
    /// 验证重复请求返回原余额并且不重复执行事务回调。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenRepeated_ShouldReturnFirstResultAndRunCallbackOnce()
    {
        using var database = new Database();
        var request = Request("order", 3);
        var first = await database.ExecuteAsync(request);
        await database.ExecuteAsync(Request("other", 2));
        var repeated = await database.ExecuteAsync(request);
        Assert.True(first.Applied);
        Assert.False(first.Duplicate);
        Assert.True(repeated.Duplicate);
        Assert.Equal(new long?[] { 7 }, repeated.RemainingQuantities);
        Assert.Equal(5, await database.QuantityAsync("a"));
        Assert.Equal(2, await database.CountAsync("audit_test"));
        Assert.Equal(2, await database.CountAsync("outbox_test"));
        Assert.Equal(2, await database.CountAsync("bing_inventory_order"));
    }

    /// <summary>
    /// 验证不同明细顺序仍代表相同整单请求。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenLinesAreReordered_ShouldRecognizeSameRequest()
    {
        using var database = new Database();
        var first = new InventoryOrderRequest("tenant", "warehouse", "order",
            new[] { new InventoryOrderLine("b", 2), new InventoryOrderLine("a", 3) });
        var second = new InventoryOrderRequest("tenant", "warehouse", "order", first.Lines.Reverse());
        await database.ExecuteAsync(first);
        var repeated = await database.ExecuteAsync(second);
        Assert.True(repeated.Duplicate);
        Assert.Equal(new long?[] { 7, 8 }, repeated.RemainingQuantities);
        Assert.Equal(1, await database.CountAsync("audit_test"));
    }

    /// <summary>
    /// 验证相同身份不能复用为不同数量的请求。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenOrderContentConflicts_ShouldRejectWithoutNewWrites()
    {
        using var database = new Database();
        await database.ExecuteAsync(Request("order", 3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.ExecuteAsync(Request("order", 4)));
        Assert.Equal(7, await database.QuantityAsync("a"));
        Assert.Equal(1, await database.CountAsync("audit_test"));
        Assert.Equal(1, await database.CountAsync("outbox_test"));
    }

    /// <summary>
    /// 验证后项不足回滚前项并且重复请求保留首次不足结果。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenLaterSkuIsInsufficient_ShouldRollbackAllItemsAndRememberResult()
    {
        using var database = new Database();
        var request = new InventoryOrderRequest("tenant", "warehouse", "order",
            new[] { new InventoryOrderLine("a", 3), new InventoryOrderLine("b", 11) });
        var result = await database.ExecuteAsync(request);
        Assert.Equal(InventoryDeductionStatus.Insufficient, result.Status);
        Assert.Empty(result.RemainingQuantities);
        Assert.Equal(10, await database.QuantityAsync("a"));
        Assert.Equal(10, await database.QuantityAsync("b"));
        Assert.Equal(0, await database.CountAsync("bing_inventory_request"));
        database.Run("UPDATE bing_inventory_stock SET Quantity = 20 WHERE Sku = 'b'");
        var repeated = await database.ExecuteAsync(request);
        Assert.True(repeated.Duplicate);
        Assert.False(repeated.Applied);
        Assert.Equal(20, await database.QuantityAsync("b"));
        Assert.Equal(1, await database.CountAsync("audit_test"));
        Assert.Equal(1, await database.CountAsync("outbox_test"));
    }

    /// <summary>
    /// 验证完成回调异常与取消均使整笔事务回滚。
    /// </summary>
    /// <param name="cancel">是否在回调中请求取消。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenCallbackFails_ShouldRollbackInventoryOrderAuditAndOutbox(bool cancel)
    {
        using var database = new Database();
        using var cancellation = new CancellationTokenSource();
        var original = new InvalidOperationException("callback failed");
        var failure = await Record.ExceptionAsync(() => database.ExecuteAsync(Request("order", 3), async transaction =>
        {
            await Database.InsertAsync(transaction, "audit_test");
            await Database.InsertAsync(transaction, "outbox_test");
            if (cancel)
                cancellation.Cancel();
            else
                throw original;
        }, cancellation.Token));
        if (cancel)
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else
            Assert.Same(original, failure);
        Assert.Equal(10, await database.QuantityAsync("a"));
        foreach (var table in new[] { "bing_inventory_order", "bing_inventory_request", "audit_test", "outbox_test" })
            Assert.Equal(0, await database.CountAsync(table));
        var retry = await database.ExecuteAsync(Request("order", 3));
        Assert.False(retry.Duplicate);
        Assert.Equal(7, await database.QuantityAsync("a"));
    }

    /// <summary>
    /// 验证预先取消不登记整单或调用完成回调。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeStart_ShouldNotWrite()
    {
        using var database = new Database();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            database.ExecuteAsync(Request("order", 1), cancellationToken: cancellation.Token));
        Assert.Equal(0, await database.CountAsync("bing_inventory_order"));
        Assert.Equal(0, await database.CountAsync("audit_test"));
    }

    /// <summary>
    /// 验证二进制整单身份不会合并大小写和尾空格。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenOrderIdsDifferByCaseOrTrailingSpace_ShouldKeepSeparateResults()
    {
        using var database = new Database();
        foreach (var id in new[] { "Order", "order", "order " })
            Assert.False((await database.ExecuteAsync(Request(id, 1))).Duplicate);
        Assert.Equal(3, await database.CountAsync("bing_inventory_order"));
        Assert.Equal(7, await database.QuantityAsync("a"));
    }

    /// <summary>
    /// 验证明细构造和整单快照在数据库操作之前校验。
    /// </summary>
    [Fact]
    public void Request_ShouldValidateLinesAndKeepImmutableSnapshot()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryOrderLine("sku", 0));
        Assert.Throws<ArgumentException>(() => new InventoryOrderRequest("tenant", "warehouse", "order",
            Array.Empty<InventoryOrderLine>()));
        Assert.Throws<ArgumentException>(() => new InventoryOrderRequest("tenant", "warehouse", "order",
            new[] { new InventoryOrderLine("a", 1), new InventoryOrderLine("a", 2) }));
        Assert.Throws<EncoderFallbackException>(() => new InventoryOrderRequest("tenant", "warehouse", "\ud800",
            new[] { new InventoryOrderLine("a", 1) }));
        var lines = new List<InventoryOrderLine> { new("a", 1) };
        var request = new InventoryOrderRequest("tenant", "warehouse", "order", lines);
        lines.Clear();
        Assert.Single(request.Lines);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InventoryOrderResult(InventoryDeductionStatus.Pending, false, Array.Empty<long?>()));
    }

    /// <summary>
    /// 验证整单存储注册幂等并保留调用方实现。
    /// </summary>
    [Fact]
    public void AddBingInventoryOrders_ShouldKeepCustomStoreAndSingleRegistration()
    {
        var services = new ServiceCollection();
        var custom = new DbInventoryOrderStore(InventoryDatabase.Sqlite, new DbInventoryStore(InventoryDatabase.Sqlite));
        services.AddSingleton<IInventoryOrderStore>(custom);
        services.AddBingInventoryOrders(InventoryDatabase.Sqlite).AddBingInventoryOrders(InventoryDatabase.Sqlite);
        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<IInventoryOrderStore>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderProjectionStore));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ServiceCollection().AddBingInventoryOrders((InventoryDatabase)99));
    }

    /// <summary>
    /// 验证整单存储复用当前作用域依赖并在作用域结束时释放一次。
    /// </summary>
    [Fact]
    public async Task AddBingInventoryOrders_WithScopedInventoryStore_ShouldIsolateScopesAndDisposeOnce()
    {
        var services = new ServiceCollection();
        services.AddScoped<IInventoryStore>(_ => new ScopedInventoryStore());
        services.AddBingInventoryOrders(InventoryDatabase.Sqlite).AddBingInventoryOrders(InventoryDatabase.Sqlite);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true, ValidateOnBuild = true
        });
        using var database = new Database();
        IInventoryOrderStore firstOrders;
        ScopedInventoryStore firstInventory;
        await using (var firstScope = provider.CreateAsyncScope())
        {
            firstOrders = firstScope.ServiceProvider.GetRequiredService<IInventoryOrderStore>();
            firstInventory = Assert.IsType<ScopedInventoryStore>(
                firstScope.ServiceProvider.GetRequiredService<IInventoryStore>());
            Assert.Same(firstOrders, firstScope.ServiceProvider.GetRequiredService<IInventoryOrderStore>());
            await database.ExecuteAsync(Request("first-scope", 3), store: firstOrders);
            Assert.Equal(1, firstInventory.Calls);
            Assert.Equal(0, firstInventory.DisposeCount);
        }
        Assert.Equal(1, firstInventory.DisposeCount);
        ScopedInventoryStore secondInventory;
        await using (var secondScope = provider.CreateAsyncScope())
        {
            var secondOrders = secondScope.ServiceProvider.GetRequiredService<IInventoryOrderStore>();
            secondInventory = Assert.IsType<ScopedInventoryStore>(
                secondScope.ServiceProvider.GetRequiredService<IInventoryStore>());
            Assert.NotSame(firstOrders, secondOrders);
            Assert.NotSame(firstInventory, secondInventory);
            await database.ExecuteAsync(Request("second-scope", 3), store: secondOrders);
            Assert.Equal(1, secondInventory.Calls);
        }
        Assert.Equal(1, secondInventory.DisposeCount);
        Assert.Equal(1, firstInventory.Calls);
        Assert.Equal(4, await database.QuantityAsync("a"));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IInventoryOrderStore));
    }

    /// <summary>
    /// 记录真实库存存储调用及当前作用域的资源释放。
    /// </summary>
    private sealed class ScopedInventoryStore : IInventoryStore, IDisposable
    {
        /// <summary>
        /// 本作用域使用的真实 SQLite 库存存储。
        /// </summary>
        private readonly IInventoryStore _inner = new DbInventoryStore(InventoryDatabase.Sqlite);

        /// <summary>
        /// 初始化一个 <see cref="ScopedInventoryStore" /> 类型的实例。
        /// </summary>
        internal ScopedInventoryStore()
        {
        }

        /// <summary>
        /// 获取当前作用域实际扣减调用次数。
        /// </summary>
        internal int Calls { get; private set; }

        /// <summary>
        /// 获取容器释放当前存储的次数。
        /// </summary>
        internal int DisposeCount { get; private set; }

        /// <inheritdoc />
        public Task<InventoryDeductionResult> TryDeductAsync(DbTransaction transaction,
            InventoryDeductionRequest request, CancellationToken cancellationToken = default)
        {
            if (DisposeCount != 0)
                throw new ObjectDisposedException(nameof(ScopedInventoryStore));
            Calls++;
            return _inner.TryDeductAsync(transaction, request, cancellationToken);
        }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 创建默认仓库的单 SKU 整单请求。
    /// </summary>
    /// <param name="id">整单标识。</param>
    /// <param name="quantity">正数扣减数量。</param>
    /// <returns>不可变整单请求。</returns>
    private static InventoryOrderRequest Request(string id, long quantity) =>
        new("tenant", "warehouse", id, new[] { new InventoryOrderLine("a", quantity) });

    /// <summary>
    /// 提供独立内存数据库的事务验证环境。
    /// </summary>
    private sealed class Database : IDisposable
    {
        /// <summary>
        /// 保持共享内存数据库存活的连接。
        /// </summary>
        private readonly SqliteConnection _anchor;
        /// <summary>
        /// 本夹具专属连接字符串。
        /// </summary>
        private readonly string _connectionString = $"Data Source=order-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        /// <summary>
        /// 当前数据库的真实库存与整单存储。
        /// </summary>
        private readonly DbInventoryOrderStore _store =
            new(InventoryDatabase.Sqlite, new DbInventoryStore(InventoryDatabase.Sqlite));

        /// <summary>
        /// 初始化一个 <see cref="Database" /> 类型的实例。
        /// </summary>
        internal Database()
        {
            _anchor = new SqliteConnection(_connectionString);
            _anchor.Open();
            foreach (var sql in InventorySchema.GetCreateStatements(InventoryDatabase.Sqlite)
                         .Concat(InventoryOrderSchema.GetCreateStatements(InventoryDatabase.Sqlite)))
                Run(sql);
            Run("CREATE TABLE audit_test (Id INTEGER PRIMARY KEY)");
            Run("CREATE TABLE outbox_test (Id INTEGER PRIMARY KEY)");
            foreach (var sku in new[] { "a", "b" })
                Run($"INSERT INTO bing_inventory_stock VALUES ('tenant','warehouse','{sku}',10)");
        }

        /// <summary>
        /// 执行整单并模拟调用方的真实提交或回滚。
        /// </summary>
        /// <param name="request">整单请求。</param>
        /// <param name="callback">可选故障回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="store">可选的当前作用域整单存储。</param>
        /// <returns>整单结果。</returns>
        internal async Task<InventoryOrderResult> ExecuteAsync(InventoryOrderRequest request,
            Func<DbTransaction, Task> callback = null, CancellationToken cancellationToken = default,
            IInventoryOrderStore store = null)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var result = await (store ?? _store).ExecuteAsync(transaction, request, async (current, _, _) =>
                {
                    if (callback != null)
                        await callback(current);
                    else
                    {
                        await InsertAsync(current, "audit_test");
                        await InsertAsync(current, "outbox_test");
                    }
                }, cancellationToken);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 插入一条当前事务内的可观察记录。
        /// </summary>
        /// <param name="transaction">当前事务。</param>
        /// <param name="table">仅来自测试常量的表名。</param>
        internal static async Task InsertAsync(DbTransaction transaction, string table)
        {
            await using var command = transaction.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {table} DEFAULT VALUES";
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 读取指定 SKU 的库存余额。
        /// </summary>
        /// <param name="sku">测试 SKU。</param>
        /// <returns>当前库存余额。</returns>
        internal async Task<long> QuantityAsync(string sku)
        {
            await using var command = _anchor.CreateCommand();
            command.CommandText = "SELECT Quantity FROM bing_inventory_stock WHERE Sku=@sku";
            command.Parameters.AddWithValue("@sku", sku);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// 使用独立连接查询已提交的首次结果。
        /// </summary>
        /// <param name="request">原整单请求。</param>
        /// <returns>首次结果；不存在时为 null。</returns>
        internal async Task<InventoryOrderResult> FindAsync(InventoryOrderRequest request)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            return await _store.FindAsync(connection, request);
        }

        /// <summary>
        /// 读取测试表的记录数量。
        /// </summary>
        /// <param name="table">仅来自测试常量的表名。</param>
        /// <returns>记录数量。</returns>
        internal async Task<long> CountAsync(string table)
        {
            await using var command = _anchor.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table}";
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// 执行不包含外部输入的夹具 SQL。
        /// </summary>
        /// <param name="sql">夹具语句。</param>
        internal void Run(string sql)
        {
            using var command = _anchor.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        /// <inheritdoc />
        public void Dispose() => _anchor.Dispose();
    }
}
