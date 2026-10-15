using System.Data.Common;
using Bing.Data.Events.Ordering;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件状态查询不修改持久化数据。
/// </summary>
public sealed class OrderedEventStateQueryTest
{
    /// <summary>
    /// 验证 SQLite 只读事务可以读取已存在和未知业务流。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenConnectionIsReadOnly_ShouldReturnStateWithoutWriting()
    {
        await using var connection = await CreateConnectionAsync();
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
        var existing = new OrderedEventStream("tenant", "inventory", "existing");
        var unknown = new OrderedEventStream("tenant", "inventory", "unknown");
        await InTransactionAsync(connection, transaction => store.EnqueueAsync(transaction, "consumer", existing,
            new OrderedEventMessage("event-3", 3, "payload")));
        await ExecuteAsync(connection, "PRAGMA query_only = ON");

        // SQLite 默认立即事务会在 query_only 模式下尝试取得写锁；延迟事务只在查询时取得读锁。
        await using (var transaction = connection.BeginTransaction(deferred: true))
        {
            var pending = await store.GetStateAsync(transaction, "consumer", existing);
            Assert.Equal(0, pending.LastSequence);
            Assert.Equal(1, pending.PendingCount);
            Assert.Equal(3, pending.FirstPendingSequence);
            Assert.False(pending.Failed);

            var empty = await store.GetStateAsync(transaction, "consumer", unknown);
            Assert.Equal(0, empty.LastSequence);
            Assert.Equal(0, empty.PendingCount);
            Assert.Null(empty.FirstPendingSequence);
            await transaction.CommitAsync();
        }

        Assert.Equal(1, await CountAsync(connection, "bing_event_cursor"));
        Assert.Equal(1, await CountAsync(connection, "bing_event_inbox"));
        Assert.Equal(0, await CountAsync(connection, "bing_event_sequence"));
    }

    /// <summary>
    /// 验证未知业务流查询提交后不创建游标、收件箱或序号记录。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenStreamIsUnknown_ShouldLeaveAllTablesEmpty()
    {
        await using var connection = await CreateConnectionAsync();
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
        var stream = new OrderedEventStream("tenant", "inventory", "unknown");

        await InTransactionAsync(connection, async transaction =>
        {
            var state = await store.GetStateAsync(transaction, "consumer", stream);
            Assert.Equal(0, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
            Assert.Null(state.FirstPendingSequence);
            Assert.False(state.Failed);
            Assert.Equal(0, state.Attempts);
            Assert.Null(state.LastError);
        });

        Assert.Equal(0, await CountAsync(connection, "bing_event_cursor"));
        Assert.Equal(0, await CountAsync(connection, "bing_event_inbox"));
        Assert.Equal(0, await CountAsync(connection, "bing_event_sequence"));
    }

    /// <summary>
    /// 验证缺口、失败与处理完成状态及消费者和租户隔离。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_ShouldDescribeGapFailureCompletionAndIsolation()
    {
        await using var connection = await CreateConnectionAsync();
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
        var stream = new OrderedEventStream("tenant-a", "inventory", "item");
        var otherTenant = new OrderedEventStream("tenant-b", "inventory", "item");
        await InTransactionAsync(connection, async transaction =>
        {
            await store.EnqueueAsync(transaction, "consumer-a", stream,
                new OrderedEventMessage("a-3", 3, "three"));
            await store.EnqueueAsync(transaction, "consumer-b", stream,
                new OrderedEventMessage("b-1", 1, "other-consumer"));
            await store.EnqueueAsync(transaction, "consumer-a", otherTenant,
                new OrderedEventMessage("t-1", 1, "other-tenant"));
        });
        await InTransactionAsync(connection, async transaction =>
        {
            var gap = await store.GetStateAsync(transaction, "consumer-a", stream);
            Assert.Equal(0, gap.LastSequence);
            Assert.Equal(1, gap.PendingCount);
            Assert.Equal(3, gap.FirstPendingSequence);
            Assert.False(gap.Failed);
            Assert.Equal(0, gap.Attempts);
        });
        await InTransactionAsync(connection, async transaction =>
        {
            await store.EnqueueAsync(transaction, "consumer-a", stream,
                new OrderedEventMessage("a-1", 1, "one"));
            await store.EnqueueAsync(transaction, "consumer-a", stream,
                new OrderedEventMessage("a-2", 2, "two"));
            await store.RecordFailureAsync(transaction, "consumer-a", stream, 1, "HANDLER_ERROR");
            var failed = await store.GetStateAsync(transaction, "consumer-a", stream);
            Assert.Equal(0, failed.LastSequence);
            Assert.Equal(3, failed.PendingCount);
            Assert.Equal(1, failed.FirstPendingSequence);
            Assert.True(failed.Failed);
            Assert.Equal(1, failed.Attempts);
            Assert.Equal("HANDLER_ERROR", failed.LastError);
        });
        await InTransactionAsync(connection, async transaction =>
        {
            await store.RetryAsync(transaction, "consumer-a", stream);
            for (var sequence = 1; sequence <= 3; sequence++)
                Assert.Equal(OrderedEventProcessingResult.Processed,
                    await store.ProcessNextAsync(transaction, "consumer-a", stream,
                        (_, _, _) => Task.CompletedTask));
            var completed = await store.GetStateAsync(transaction, "consumer-a", stream);
            Assert.Equal(3, completed.LastSequence);
            Assert.Equal(0, completed.PendingCount);
            Assert.Null(completed.FirstPendingSequence);
            Assert.False(completed.Failed);
            Assert.Null(completed.LastError);

            var otherConsumerState = await store.GetStateAsync(transaction, "consumer-b", stream);
            var otherTenantState = await store.GetStateAsync(transaction, "consumer-a", otherTenant);
            Assert.Equal(0, otherConsumerState.LastSequence);
            Assert.Equal(1, otherConsumerState.PendingCount);
            Assert.Equal(1, otherConsumerState.FirstPendingSequence);
            Assert.Equal(0, otherTenantState.LastSequence);
            Assert.Equal(1, otherTenantState.PendingCount);
            Assert.Equal(1, otherTenantState.FirstPendingSequence);
        });
    }

    /// <summary>
    /// 建立内存数据库并运行生产建表语句。
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        foreach (var sql in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.Sqlite))
            await ExecuteAsync(connection, sql);
        return connection;
    }

    /// <summary>
    /// 在独立事务中运行测试操作。
    /// </summary>
    private static async Task InTransactionAsync(SqliteConnection connection, Func<DbTransaction, Task> action)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await action(transaction);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 执行固定的测试数据库语句。
    /// </summary>
    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 读取指定测试表中的行数。
    /// </summary>
    private static async Task<long> CountAsync(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
