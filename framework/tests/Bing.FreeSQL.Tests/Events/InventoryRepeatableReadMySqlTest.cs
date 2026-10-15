using System.Data;
using Bing.Data.Inventory;
using Bing.Test.Shared;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证真实 MySQL 库存重复请求在不同事务快照中的结果。
/// </summary>
[Collection("MySqlOrderedEvents")]
public sealed class InventoryRepeatableReadMySqlTest
{
    /// <summary>
    /// 重复读旧快照下重复请求仍返回首次提交的结果。
    /// </summary>
    [IntegrationFact("MySql")]
    public Task TryDeductAsync_WhenRepeatableReadSnapshotPredatesCommit_ShouldReturnCommittedResult() =>
        VerifyDuplicateAsync(IsolationLevel.RepeatableRead);

    /// <summary>
    /// 读已提交事务下重复请求仍返回首次提交的结果。
    /// </summary>
    [IntegrationFact("MySql")]
    public Task TryDeductAsync_WhenReadCommittedSnapshotPredatesCommit_ShouldReturnCommittedResult() =>
        VerifyDuplicateAsync(IsolationLevel.ReadCommitted);

    /// <summary>
    /// 重复读旧快照下相同请求标识不能改用另一扣减数量。
    /// </summary>
    [IntegrationFact("MySql")]
    public Task TryDeductAsync_WhenStaleSnapshotRequestHasDifferentQuantity_ShouldRejectIdentityConflict() =>
        VerifyDuplicateAsync(IsolationLevel.RepeatableRead, conflictingQuantity: true);

    /// <summary>
    /// 在两个交错的真实事务中验证扣减及重复结果。
    /// </summary>
    /// <param name="isolationLevel">第二个事务的隔离级别。</param>
    /// <param name="conflictingQuantity">是否用不同数量复用首个请求标识。</param>
    private static async Task VerifyDuplicateAsync(IsolationLevel isolationLevel, bool conflictingQuantity = false)
    {
        var options = new MySqlConnectionStringBuilder(IntegrationTestConnectionStringResolver.Resolve("MySql"))
        {
            Pooling = false
        };
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(options.ConnectionString, "MySql");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var firstConnection = new MySqlConnection(options.ConnectionString);
        await using var secondConnection = new MySqlConnection(options.ConnectionString);
        await firstConnection.OpenAsync(timeout.Token);
        await secondConnection.OpenAsync(timeout.Token);
        foreach (var sql in InventorySchema.GetCreateStatements(InventoryDatabase.MySql))
        {
            using var schema = new MySqlCommand(sql, firstConnection);
            await schema.ExecuteNonQueryAsync(timeout.Token);
        }

        var tenant = "inventory-snapshot-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var seed = new MySqlCommand("INSERT INTO bing_inventory_stock " +
                "(TenantId, WarehouseId, Sku, Quantity) VALUES (@tenant, 'warehouse', 'sku', 10)", firstConnection))
            {
                seed.Parameters.AddWithValue("@tenant", tenant);
                await seed.ExecuteNonQueryAsync(timeout.Token);
            }

            var request = new InventoryDeductionRequest(tenant, "warehouse", "sku", 3, "same-request");
            var store = new DbInventoryStore(InventoryDatabase.MySql);
            await using var firstTransaction = await firstConnection.BeginTransactionAsync(timeout.Token);
            var first = await store.TryDeductAsync(firstTransaction, request, timeout.Token);
            Assert.True(first.Applied);
            Assert.False(first.Duplicate);
            Assert.Equal(7, first.RemainingQuantity);

            await using var secondTransaction = await secondConnection.BeginTransactionAsync(isolationLevel, timeout.Token);
            // 在首个请求提交前创建真实一致性快照，避免用调度延时推测事务交错。
            using (var snapshot = new MySqlCommand("SELECT COUNT(*) FROM bing_inventory_request " +
                "WHERE TenantId = @tenant AND RequestId = @request", secondConnection, secondTransaction))
            {
                snapshot.Parameters.AddWithValue("@tenant", tenant);
                snapshot.Parameters.AddWithValue("@request", request.RequestId);
                Assert.Equal(0L, Convert.ToInt64(await snapshot.ExecuteScalarAsync(timeout.Token)));
            }
            await firstTransaction.CommitAsync(timeout.Token);

            if (conflictingQuantity)
            {
                var conflict = new InventoryDeductionRequest(tenant, "warehouse", "sku", 4, request.RequestId);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    store.TryDeductAsync(secondTransaction, conflict, timeout.Token));
                await secondTransaction.RollbackAsync(timeout.Token);
            }
            else
            {
                var duplicate = await store.TryDeductAsync(secondTransaction, request, timeout.Token);
                Assert.True(duplicate.Applied);
                Assert.True(duplicate.Duplicate);
                Assert.Equal(first.RemainingQuantity, duplicate.RemainingQuantity);
                await secondTransaction.CommitAsync(timeout.Token);
            }

            using (var quantity = new MySqlCommand("SELECT Quantity FROM bing_inventory_stock " +
                "WHERE TenantId = @tenant AND WarehouseId = 'warehouse' AND Sku = 'sku'", firstConnection))
            {
                quantity.Parameters.AddWithValue("@tenant", tenant);
                Assert.Equal(7L, Convert.ToInt64(await quantity.ExecuteScalarAsync(timeout.Token)));
            }
            using (var receipts = new MySqlCommand("SELECT COUNT(*) FROM bing_inventory_request " +
                "WHERE TenantId = @tenant", firstConnection))
            {
                receipts.Parameters.AddWithValue("@tenant", tenant);
                Assert.Equal(1L, Convert.ToInt64(await receipts.ExecuteScalarAsync(timeout.Token)));
            }
        }
        finally
        {
            // 只清理本测试随机租户的数据，不重置共享测试库或其他业务表。
            foreach (var table in new[] { "bing_inventory_request", "bing_inventory_stock" })
            {
                using var cleanup = new MySqlCommand($"DELETE FROM {table} WHERE TenantId = @tenant", firstConnection);
                cleanup.Parameters.AddWithValue("@tenant", tenant);
                await cleanup.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
    }
}
