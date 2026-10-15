using System.Data;
using Bing.Events.Consumption;
using Bing.Test.Shared;
using MySqlConnector;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证 MySQL 消费收据只识别目标消息的重复键。
/// </summary>
public sealed class EventConsumptionMySqlIntegrationTest
{
    /// <summary>
    /// 验证目标收据重复时返回 false 且保留首次写入。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task TryRecordAsync_WhenTargetExists_ShouldReturnFalse()
    {
        var consumer = NewKey();
        await WithDatabaseAsync(new[] { consumer }, async connection =>
        {
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            var eventId = NewKey();
            await using var transaction = await connection.BeginTransactionAsync();

            Assert.True(await store.TryRecordAsync(transaction, consumer, eventId));
            Assert.False(await store.TryRecordAsync(transaction, consumer, eventId));
            Assert.Equal(1, await CountReceiptsAsync(connection, transaction, consumer));
            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// 验证检查约束失败仍传播原始数据库错误。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task TryRecordAsync_WhenCheckFails_ShouldPropagateError()
    {
        var consumer = NewKey();
        var constraint = "chk_bing_receipt_" + Guid.NewGuid().ToString("N");
        await WithDatabaseAsync(new[] { consumer }, async connection =>
        {
            await ExecuteAsync(connection, $"ALTER TABLE bing_event_consumption ADD CONSTRAINT `{constraint}` " +
                $"CHECK (Consumer <> '{consumer}')");
            try
            {
                var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
                await using var transaction = await connection.BeginTransactionAsync();
                var error = await Assert.ThrowsAsync<MySqlException>(() =>
                    store.TryRecordAsync(transaction, consumer, NewKey()));
                Assert.Equal(3819, error.Number);
                Assert.Equal(0, await CountReceiptsAsync(connection, transaction, consumer));
                await transaction.RollbackAsync();
            }
            finally
            {
                await ExecuteAsync(connection,
                    $"ALTER TABLE bing_event_consumption DROP CHECK `{constraint}`");
            }
        });
    }

    /// <summary>
    /// 验证其他唯一键冲突不被误判为目标收据重复。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task TryRecordAsync_WhenOtherUniqueKeyConflicts_ShouldPropagateError()
    {
        var firstConsumer = NewKey();
        var secondConsumer = NewKey();
        var column = "ReceiptProbe" + Guid.NewGuid().ToString("N");
        var index = "ux_receipt_probe_" + Guid.NewGuid().ToString("N");
        await WithDatabaseAsync(new[] { firstConsumer, secondConsumer }, async connection =>
        {
            await ExecuteAsync(connection, $"ALTER TABLE bing_event_consumption ADD COLUMN `{column}` " +
                $"varchar(128) GENERATED ALWAYS AS " +
                $"(CASE WHEN Consumer IN ('{firstConsumer}', '{secondConsumer}') THEN EventId ELSE NULL END) STORED");
            try
            {
                await ExecuteAsync(connection,
                    $"ALTER TABLE bing_event_consumption ADD UNIQUE INDEX `{index}` (`{column}`)");
                try
                {
                    var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
                    var eventId = NewKey();
                    await using var transaction = await connection.BeginTransactionAsync();
                    Assert.True(await store.TryRecordAsync(transaction, firstConsumer, eventId));

                    var error = await Assert.ThrowsAsync<MySqlException>(() =>
                        store.TryRecordAsync(transaction, secondConsumer, eventId));

                    Assert.Equal(1062, error.Number);
                    Assert.Equal(1, await CountReceiptsAsync(connection, transaction, firstConsumer));
                    Assert.Equal(0, await CountReceiptsAsync(connection, transaction, secondConsumer));
                    await transaction.CommitAsync();
                }
                finally
                {
                    await ExecuteAsync(connection,
                        $"ALTER TABLE bing_event_consumption DROP INDEX `{index}`");
                }
            }
            finally
            {
                await ExecuteAsync(connection,
                    $"ALTER TABLE bing_event_consumption DROP COLUMN `{column}`");
            }
        });
    }

    /// <summary>
    /// 验证旧事务快照下的当前读仍识别已提交目标收据。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task TryRecordAsync_WhenSnapshotIsStale_ShouldReturnFalseForCommittedTarget()
    {
        var consumer = NewKey();
        await WithDatabaseAsync(new[] { consumer }, async connection =>
        {
            var eventId = NewKey();
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            // 已打开连接的 ConnectionString 默认不再包含口令，第二连接使用原始测试配置。
            await using var snapshotConnection = new MySqlConnection(
                IntegrationTestConnectionStringResolver.Resolve("MySql"));
            await snapshotConnection.OpenAsync();
            await using var snapshot = await snapshotConnection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            Assert.Equal(0, await CountReceiptsAsync(snapshotConnection, snapshot, consumer));

            await using (var committed = await connection.BeginTransactionAsync())
            {
                Assert.True(await store.TryRecordAsync(committed, consumer, eventId));
                await committed.CommitAsync();
            }

            Assert.False(await store.TryRecordAsync(snapshot, consumer, eventId));
            await snapshot.RollbackAsync();
            Assert.Equal(1, await CountReceiptsAsync(connection, null, consumer));
        });
    }

    /// <summary>
    /// 验证 MySQL 收据清理只删除目标消费者的过期记录。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceipts()
    {
        var consumer = NewKey();
        var otherConsumer = NewKey();
        await WithDatabaseAsync(new[] { consumer, otherConsumer }, async connection =>
        {
            var store = new EventConsumptionStore(EventConsumptionDatabase.MySql);
            var now = DateTime.UtcNow;
            await using var transaction = await connection.BeginTransactionAsync();

            Assert.True(await store.TryRecordAsync(transaction, consumer, "old-" + NewKey()));
            Assert.True(await store.TryRecordAsync(transaction, consumer, "new-" + NewKey()));
            Assert.True(await store.TryRecordAsync(transaction, otherConsumer, "old-" + NewKey()));
            using (var update = new MySqlCommand(
                       "UPDATE bing_event_consumption SET ProcessedAtUtc = @processedAtUtc " +
                       "WHERE Consumer = @consumer AND EventId LIKE @eventPrefix", connection, transaction))
            {
                update.Parameters.AddWithValue("@processedAtUtc", now.AddHours(-2));
                update.Parameters.AddWithValue("@consumer", consumer);
                update.Parameters.AddWithValue("@eventPrefix", "old-%");
                await update.ExecuteNonQueryAsync();
            }
            using (var update = new MySqlCommand(
                       "UPDATE bing_event_consumption SET ProcessedAtUtc = @processedAtUtc " +
                       "WHERE Consumer = @consumer", connection, transaction))
            {
                update.Parameters.AddWithValue("@processedAtUtc", now.AddHours(-2));
                update.Parameters.AddWithValue("@consumer", otherConsumer);
                await update.ExecuteNonQueryAsync();
            }

            Assert.Equal(1, await store.DeleteBeforeAsync(transaction, consumer, now.AddHours(-1), 10));
            Assert.Equal(1, await CountReceiptsAsync(connection, transaction, consumer));
            Assert.Equal(1, await CountReceiptsAsync(connection, transaction, otherConsumer));
            await transaction.CommitAsync();
        });
    }

    /// <summary>
    /// 在受保护的专用测试库运行并清除本测试产生的收据。
    /// </summary>
    private static async Task WithDatabaseAsync(string[] consumers, Func<MySqlConnection, Task> action)
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var statement in EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.MySql))
            await ExecuteAsync(connection, statement);
        try
        {
            await action(connection);
        }
        finally
        {
            foreach (var consumer in consumers)
            {
                using var cleanup = new MySqlCommand(
                    "DELETE FROM bing_event_consumption WHERE Consumer = @consumer", connection);
                cleanup.Parameters.AddWithValue("@consumer", consumer);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// 读取指定消费者的收据数量。
    /// </summary>
    private static async Task<long> CountReceiptsAsync(MySqlConnection connection,
        MySqlTransaction transaction, string consumer)
    {
        using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM bing_event_consumption WHERE Consumer = @consumer", connection, transaction);
        command.Parameters.AddWithValue("@consumer", consumer);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 执行测试专用表结构变更。
    /// </summary>
    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 生成与其他集成测试隔离的标识。
    /// </summary>
    private static string NewKey() => Guid.NewGuid().ToString("N");
}
