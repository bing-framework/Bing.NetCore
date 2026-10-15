using System.Data.Common;
using Bing.Data.Inventory;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证库存条件扣减和请求幂等行为。
/// </summary>
public sealed class InventoryStoreTest
{
    /// <summary>
    /// 成功扣减与重复请求返回同一结果。
    /// </summary>
    [Fact]
    public async Task TryDeductAsync_WhenRequestRepeats_ShouldApplyOnce()
    {
        using var fixture = new InventorySqliteFixture(10);
        var request = new InventoryDeductionRequest("tenant", "warehouse", "sku", 3, "request-1");
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);

        var first = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, request));
        var second = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, request));

        Assert.True(first.Applied);
        Assert.False(first.Duplicate);
        Assert.Equal(7, first.RemainingQuantity);
        Assert.True(second.Applied);
        Assert.True(second.Duplicate);
        Assert.Equal(first.RemainingQuantity, second.RemainingQuantity);
        Assert.Equal(7, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 余额不足时不允许库存变为负数并记住失败结果。
    /// </summary>
    [Fact]
    public async Task TryDeductAsync_WhenQuantityIsInsufficient_ShouldRemainNonNegative()
    {
        using var fixture = new InventorySqliteFixture(2);
        var request = new InventoryDeductionRequest("tenant", "warehouse", "sku", 3, "request-2");
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);

        var first = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, request));
        var second = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, request));

        Assert.Equal(InventoryDeductionStatus.Insufficient, first.Status);
        Assert.False(first.Applied);
        Assert.True(second.Duplicate);
        Assert.Equal(InventoryDeductionStatus.Insufficient, second.Status);
        Assert.Equal(2, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 同一请求标识不能复用到不同 SKU 或数量。
    /// </summary>
    [Fact]
    public async Task TryDeductAsync_WhenRequestIdentityConflicts_ShouldReject()
    {
        using var fixture = new InventorySqliteFixture(10);
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);
        var first = new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "request-3");
        var conflicting = new InventoryDeductionRequest("tenant", "warehouse", "other-sku", 1, "request-3");
        await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, first));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, conflicting)));
        Assert.Equal(9, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 调用方回滚时库存和请求结果一起回滚。
    /// </summary>
    [Fact]
    public async Task TryDeductAsync_WhenCallerRollsBack_ShouldNotConsumeOrReserveRequest()
    {
        using var fixture = new InventorySqliteFixture(10);
        var request = new InventoryDeductionRequest("tenant", "warehouse", "sku", 4, "request-4");
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync<object>(async transaction =>
        {
            var result = await store.TryDeductAsync(transaction, request);
            Assert.True(result.Applied);
            throw new InvalidOperationException("business failure");
            // 仅用于满足带结果事务委托的返回类型。
            #pragma warning disable CS0162
            return null;
            #pragma warning restore CS0162
        }));
        var retry = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, request));

        Assert.True(retry.Applied);
        Assert.False(retry.Duplicate);
        Assert.Equal(6, retry.RemainingQuantity);
        Assert.Equal(6, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 库存存储注册使用单例且不会重复覆盖调用方实例。
    /// </summary>
    [Fact]
    public void AddBingInventoryStore_ShouldRegisterSingletonWithoutReplacement()
    {
        var services = new ServiceCollection();
        var custom = new DbInventoryStore(InventoryDatabase.Sqlite);
        services.AddSingleton<IInventoryStore>(custom);
        services.AddBingInventoryStore(InventoryDatabase.Sqlite);

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<IInventoryStore>());
        Assert.Equal(ServiceLifetime.Singleton,
            Assert.Single(services, item => item.ServiceType == typeof(IInventoryStore)).Lifetime);
    }

    /// <summary>
    /// 批量扣减按稳定 SKU 顺序调用存储并返回结果。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenRequestsAreUnsorted_ShouldUseStableOrder()
    {
        using var fixture = new InventorySqliteFixture(1);
        var calls = new List<string>();
        var store = new RecordingInventoryStore(calls);
        var requests = new[]
        {
            new InventoryDeductionRequest("tenant", "warehouse", "sku-2", 1, "batch-2"),
            new InventoryDeductionRequest("tenant", "warehouse", "sku-1", 1, "batch-1")
        };

        var results = await fixture.ExecuteAsync(transaction => InventoryBatchOperations.DeductAsync(
            transaction, store, requests, CancellationToken.None));

        Assert.Equal(new[] { "sku-1", "sku-2" }, calls);
        Assert.Equal(2, results.Count);
    }

    /// <summary>
    /// 租户和请求标识包含分隔字符时仍按结构化二元键独立处理。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenTenantAndRequestKeysContainDelimiter_ShouldProcessBoth()
    {
        const string firstTenant = "a\u001fb";
        const string firstRequestId = "c";
        const string secondTenant = "a";
        const string secondRequestId = "b\u001fc";
        using var fixture = new InventorySqliteFixture(10, firstTenant, secondTenant);
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);
        var requests = new[]
        {
            new InventoryDeductionRequest(firstTenant, "warehouse", "sku", 1, firstRequestId),
            new InventoryDeductionRequest(secondTenant, "warehouse", "sku", 1, secondRequestId)
        };

        var results = await fixture.ExecuteAsync(transaction => InventoryBatchOperations.DeductAsync(
            transaction, store, requests, CancellationToken.None));

        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.True(result.Applied);
            Assert.False(result.Duplicate);
        });
        Assert.Equal(9, await fixture.ReadQuantityAsync(firstTenant));
        Assert.Equal(9, await fixture.ReadQuantityAsync(secondTenant));
    }

    /// <summary>
    /// 不同租户可以独立使用相同的请求标识。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenRequestIdsMatchAcrossTenants_ShouldProcessBoth()
    {
        using var fixture = new InventorySqliteFixture(1);
        var calls = new List<string>();
        var store = new RecordingInventoryStore(calls);
        var requests = new[]
        {
            new InventoryDeductionRequest("tenant-a", "warehouse", "sku", 1, "same-request"),
            new InventoryDeductionRequest("tenant-b", "warehouse", "sku", 1, "same-request")
        };

        var results = await fixture.ExecuteAsync(transaction => InventoryBatchOperations.DeductAsync(
            transaction, store, requests, CancellationToken.None));

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { "sku", "sku" }, calls);
    }

    /// <summary>
    /// 同一租户重复请求标识会拒绝批次并回滚已完成的扣减。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenSameTenantRequestIdsRepeat_ShouldRejectAndRollback()
    {
        using var fixture = new InventorySqliteFixture(10);
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);
        var requests = new[]
        {
            new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "duplicate-request"),
            new InventoryDeductionRequest("tenant", "warehouse", "sku", 2, "duplicate-request")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync(transaction =>
            InventoryBatchOperations.DeductAsync(transaction, store, requests)));
        Assert.Equal(10, await fixture.ReadQuantityAsync());

        var retry = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, requests[0]));
        Assert.True(retry.Applied);
        Assert.False(retry.Duplicate);
        Assert.Equal(9, retry.RemainingQuantity);
        Assert.Equal(9, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 批量扣减余额不足时中止后续请求。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenOneRequestIsInsufficient_ShouldStopAndThrow()
    {
        using var fixture = new InventorySqliteFixture(1);
        var calls = new List<string>();
        var store = new RecordingInventoryStore(calls, insufficientSku: "sku-1");
        var requests = new[]
        {
            new InventoryDeductionRequest("tenant", "warehouse", "sku-1", 1, "batch-fail"),
            new InventoryDeductionRequest("tenant", "warehouse", "sku-2", 1, "batch-after")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync(transaction =>
            InventoryBatchOperations.DeductAsync(transaction, store, requests)));
        Assert.Equal(new[] { "sku-1" }, calls);
    }

    /// <summary>
    /// 批量扣减失败时回滚同一事务中已经完成的库存和请求登记。
    /// </summary>
    [Fact]
    public async Task InventoryBatch_WhenOneRequestIsInsufficient_ShouldRollbackPreviousDeductions()
    {
        using var fixture = new InventorySqliteFixture(1);
        var store = new DbInventoryStore(InventoryDatabase.Sqlite);
        var requests = new[]
        {
            new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "batch-first"),
            new InventoryDeductionRequest("tenant", "warehouse", "sku", 1, "batch-second")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExecuteAsync(transaction =>
            InventoryBatchOperations.DeductAsync(transaction, store, requests)));
        Assert.Equal(1, await fixture.ReadQuantityAsync());

        var retry = await fixture.ExecuteAsync(transaction => store.TryDeductAsync(transaction, requests[0]));
        Assert.True(retry.Applied);
        Assert.False(retry.Duplicate);
        Assert.Equal(0, retry.RemainingQuantity);
        Assert.Equal(0, await fixture.ReadQuantityAsync());
    }

    /// <summary>
    /// 提供共享内存 SQLite 库存表。
    /// </summary>
    private sealed class InventorySqliteFixture : IDisposable
    {
        /// <summary>
        /// 保持共享内存数据库存活的锚定连接。
        /// </summary>
        private readonly SqliteConnection _anchor;

        /// <summary>
        /// 共享内存 SQLite 数据库的连接字符串。
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// 初始化一个 <see cref="InventorySqliteFixture" /> 类型的实例。
        /// </summary>
        /// <param name="quantity">每个租户库存行的初始数量。</param>
        /// <param name="tenantIds">需要初始化库存行的租户标识；未指定时使用默认租户。</param>
        internal InventorySqliteFixture(long quantity, params string[] tenantIds)
        {
            _connectionString = $"Data Source=bing-inventory-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _anchor = new SqliteConnection(_connectionString);
            _anchor.Open();
            foreach (var statement in InventorySchema.GetCreateStatements(InventoryDatabase.Sqlite))
                Execute(statement);
            var tenants = tenantIds == null || tenantIds.Length == 0 ? new[] { "tenant" } : tenantIds;
            foreach (var tenantId in tenants)
                InsertStock(tenantId, quantity);
        }

        /// <summary>
        /// 在当前事务中执行操作，并在操作失败时回滚事务。
        /// </summary>
        /// <typeparam name="T">操作返回值类型。</typeparam>
        /// <param name="action">接收活动数据库事务的操作。</param>
        /// <returns>操作返回的结果。</returns>
        internal async Task<T> ExecuteAsync<T>(Func<DbTransaction, Task<T>> action)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var result = await action(transaction);
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
        /// 读取指定租户默认库存行的数量。
        /// </summary>
        /// <param name="tenantId">租户标识。</param>
        /// <returns>库存数量。</returns>
        internal async Task<long> ReadQuantityAsync(string tenantId = "tenant")
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Quantity FROM bing_inventory_stock " +
                                  "WHERE TenantId = @tenant AND WarehouseId = 'warehouse' AND Sku = 'sku'";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenant";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        /// <summary>
        /// 向共享内存数据库写入指定租户的初始库存。
        /// </summary>
        /// <param name="tenantId">租户标识。</param>
        /// <param name="quantity">库存数量。</param>
        private void InsertStock(string tenantId, long quantity)
        {
            using var command = _anchor.CreateCommand();
            command.CommandText = "INSERT INTO bing_inventory_stock (TenantId, WarehouseId, Sku, Quantity) " +
                                  "VALUES (@tenant, 'warehouse', 'sku', @quantity)";
            var tenantParameter = command.CreateParameter();
            tenantParameter.ParameterName = "@tenant";
            tenantParameter.Value = tenantId;
            command.Parameters.Add(tenantParameter);
            var quantityParameter = command.CreateParameter();
            quantityParameter.ParameterName = "@quantity";
            quantityParameter.Value = quantity;
            command.Parameters.Add(quantityParameter);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 执行一条数据库初始化语句。
        /// </summary>
        /// <param name="sql">需要执行的 SQL 语句。</param>
        private void Execute(string sql)
        {
            using var command = _anchor.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 释放共享内存数据库的锚定连接。
        /// </summary>
        public void Dispose() => _anchor.Dispose();
    }

    /// <summary>
    /// 记录批量调用顺序的测试存储。
    /// </summary>
    private sealed class RecordingInventoryStore : IInventoryStore
    {
        /// <summary>
        /// 已记录的 SKU 调用顺序。
        /// </summary>
        private readonly IList<string> _calls;

        /// <summary>
        /// 需要返回余额不足的 SKU 标识。
        /// </summary>
        private readonly string _insufficientSku;

        /// <summary>
        /// 初始化一个 <see cref="RecordingInventoryStore" /> 类型的实例。
        /// </summary>
        /// <param name="calls">接收 SKU 调用顺序的集合。</param>
        /// <param name="insufficientSku">需要返回余额不足的 SKU 标识。</param>
        internal RecordingInventoryStore(IList<string> calls, string insufficientSku = null)
        {
            _calls = calls;
            _insufficientSku = insufficientSku;
        }

        /// <inheritdoc />
        public Task<InventoryDeductionResult> TryDeductAsync(DbTransaction transaction,
            InventoryDeductionRequest request, CancellationToken cancellationToken = default)
        {
            _calls.Add(request.Sku);
            return Task.FromResult(new InventoryDeductionResult(
                request.Sku == _insufficientSku ? InventoryDeductionStatus.Insufficient : InventoryDeductionStatus.Applied,
                false, request.Sku == _insufficientSku ? null : 0));
        }
    }
}
