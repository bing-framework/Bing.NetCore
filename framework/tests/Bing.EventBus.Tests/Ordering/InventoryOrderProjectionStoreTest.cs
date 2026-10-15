using System.Text;
using Bing.Data.Events.Ordering;
using Bing.Data.Inventory;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序库存完成投影使用当前事务并保存原始正文。
/// </summary>
public sealed class InventoryOrderProjectionStoreTest
{
    /// <summary>
    /// 验证投影提交后保留租户、序号、状态和原始正文。
    /// </summary>
    [Fact]
    public async Task SaveAsync_ShouldUseCallerTransactionAndPreservePayload()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = InventoryOrderSchema.GetCreateStatements(InventoryDatabase.Sqlite)[1];
            await schema.ExecuteNonQueryAsync();
        }

        const string payload = "{\"TenantId\":\"tenant\",\"OrderId\":\"order\",\"Status\":\"Applied\"}";
        var stream = new OrderedEventStream("tenant", "inventory-order", "order");
        var message = new OrderedEventMessage("event-1", 3, payload, "v1");
        var store = new DbInventoryOrderProjectionStore(InventoryDatabase.Sqlite);
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await store.SaveAsync(transaction, "admin-inventory-v1", stream, message, "Applied", payload);
            await transaction.CommitAsync();
        }

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT Consumer, TenantId, OrderId, Sequence, EventId, Status, PayloadJson " +
                            "FROM bing_inventory_ordered_projection";
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("admin-inventory-v1", reader.GetString(0));
        Assert.Equal("tenant", Encoding.UTF8.GetString((byte[])reader[1]));
        Assert.Equal("order", Encoding.UTF8.GetString((byte[])reader[2]));
        Assert.Equal(3, reader.GetInt64(3));
        Assert.Equal("event-1", reader.GetString(4));
        Assert.Equal("Applied", reader.GetString(5));
        Assert.Equal(payload, reader.GetString(6));
        Assert.False(await reader.ReadAsync());
    }

    /// <summary>
    /// 验证投影不会接受超大正文。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenPayloadExceedsLimit_ShouldReject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new DbInventoryOrderProjectionStore(InventoryDatabase.Sqlite);
        var stream = new OrderedEventStream("tenant", "inventory-order", "order");
        var message = new OrderedEventMessage("event-1", 1, "{}", "v1");
        var payload = new string('x', 65537);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(transaction, "consumer", stream,
            message, "Applied", payload));
    }

    /// <summary>
    /// 验证投影只接受库存处理器产生的终态。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenStatusIsUnsupported_ShouldRejectBeforeWriting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var schema = connection.CreateCommand())
        {
            schema.CommandText = InventoryOrderSchema.GetCreateStatements(InventoryDatabase.Sqlite)[1];
            await schema.ExecuteNonQueryAsync();
        }

        var store = new DbInventoryOrderProjectionStore(InventoryDatabase.Sqlite);
        var stream = new OrderedEventStream("tenant", "inventory-order", "order");
        var message = new OrderedEventMessage("event-invalid-status", 1, "{}", "v1");
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(transaction, "consumer", stream,
            message, "Pending", "{}"));

        await using var query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT COUNT(*) FROM bing_inventory_ordered_projection";
        Assert.Equal(0L, (long)await query.ExecuteScalarAsync());
    }
}
