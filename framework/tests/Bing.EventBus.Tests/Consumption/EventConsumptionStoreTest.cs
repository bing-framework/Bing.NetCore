using Bing.Events.Consumption;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Bing.EventBus.Tests.Consumption;

/// <summary>
/// 验证事件消费记录与业务写入共享事务。
/// </summary>
public class EventConsumptionStoreTest
{
    /// <summary>
    /// 重复事件只为同一逻辑消费者记录一次。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_SameConsumerAndEvent_ShouldReturnFalseOnDuplicate()
    {
        using var connection = await CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);

        (await store.TryRecordAsync(transaction, "inventory", "event-1")).ShouldBeTrue();
        (await store.TryRecordAsync(transaction, "inventory", "event-1")).ShouldBeFalse();
        (await CountReceiptsAsync(connection, transaction)).ShouldBe(1);
    }

    /// <summary>
    /// 不同逻辑消费者可分别处理同一事件。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_DifferentConsumers_ShouldRecordIndependently()
    {
        using var connection = await CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);

        (await store.TryRecordAsync(transaction, "inventory", "event-1")).ShouldBeTrue();
        (await store.TryRecordAsync(transaction, "audit", "event-1")).ShouldBeTrue();
        (await CountReceiptsAsync(connection, transaction)).ShouldBe(2);
    }

    /// <summary>
    /// 验证不同 Unicode 组合形式按精确标识分别记录。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_UnicodeIdentities_ShouldRemainDistinct()
    {
        using var connection = await CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        var composed = "tenant-\u00E9";
        var decomposed = "tenant-e\u0301";

        (await store.TryRecordAsync(transaction, composed, "event-1")).ShouldBeTrue();
        (await store.TryRecordAsync(transaction, decomposed, "event-1")).ShouldBeTrue();
        (await CountReceiptsAsync(connection, transaction)).ShouldBe(2);
    }

    /// <summary>
    /// 验证非 BMP 和多字节标识按字符上限进行精确记录。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_NonBmpAndUtf8Boundary_ShouldKeepOrdinalIdentity()
    {
        using var connection = await CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        var emojiConsumer = "consumer-😀";
        var otherEmojiConsumer = "consumer-😃";
        var multibyteConsumer = new string('é', 200);
        var multibyteEvent = new string('界', 128);

        (await store.TryRecordAsync(transaction, emojiConsumer, "event-1")).ShouldBeTrue();
        (await store.TryRecordAsync(transaction, otherEmojiConsumer, "event-1")).ShouldBeTrue();
        (await store.TryRecordAsync(transaction, multibyteConsumer, multibyteEvent)).ShouldBeTrue();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => store.TryRecordAsync(transaction,
            multibyteConsumer + "a", multibyteEvent));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => store.TryRecordAsync(transaction,
            emojiConsumer, multibyteEvent + "a"));

        (await CountReceiptsAsync(connection, transaction)).ShouldBe(3);
    }

    /// <summary>
    /// 业务失败回滚后，同一事件仍可重试。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_BusinessRollback_ShouldRemoveReceiptAndAllowRetry()
    {
        using var connection = await CreateConnectionAsync();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);

        using (var transaction = connection.BeginTransaction())
        {
            (await store.TryRecordAsync(transaction, "inventory", "event-1")).ShouldBeTrue();
            await InsertBusinessRowAsync(connection, transaction);
            transaction.Rollback();
        }

        (await CountReceiptsAsync(connection)).ShouldBe(0);
        (await CountBusinessRowsAsync(connection)).ShouldBe(0);

        using (var retry = connection.BeginTransaction())
        {
            (await store.TryRecordAsync(retry, "inventory", "event-1")).ShouldBeTrue();
            await InsertBusinessRowAsync(connection, retry);
            retry.Commit();
        }

        (await CountReceiptsAsync(connection)).ShouldBe(1);
        (await CountBusinessRowsAsync(connection)).ShouldBe(1);
    }

    /// <summary>
    /// 无效标识及预先取消不会写入记录。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_InvalidKeyOrCancellation_ShouldNotWriteReceipt()
    {
        using var connection = await CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);

        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, " ", "event-1"));
        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, "inventory", ""));
        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, " inventory", "event-1"));
        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, "inventory ", "event-1"));
        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, "inventory", " event-1"));
        await Should.ThrowAsync<ArgumentException>(() => store.TryRecordAsync(transaction, "inventory", "event-1 "));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            store.TryRecordAsync(transaction, new string('a', 201), "event-1"));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            store.TryRecordAsync(transaction, "inventory", new string('a', 129)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() =>
            store.TryRecordAsync(transaction, "inventory", "event-1", cancellation.Token));

        (await CountReceiptsAsync(connection, transaction)).ShouldBe(0);
    }

    /// <summary>
    /// 验证非目标唯一键和检查约束失败不会伪装成重复消息。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TryRecordAsync_WhenOtherConstraintFails_ShouldPropagateStorageError(bool checkConstraint)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE bing_event_consumption " +
                "(Consumer TEXT NOT NULL, EventId TEXT NOT NULL, ProcessedAtUtc TEXT NOT NULL, " +
                "PRIMARY KEY (Consumer, EventId), " +
                (checkConstraint ? "CHECK (Consumer <> 'reject'))" : "UNIQUE (EventId))");
            await create.ExecuteNonQueryAsync();
        }
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        Assert.True(await store.TryRecordAsync(transaction, "first", "event"));
        Assert.False(await store.TryRecordAsync(transaction, "first", "event"));

        var error = await Assert.ThrowsAsync<SqliteException>(() => store.TryRecordAsync(transaction,
            checkConstraint ? "reject" : "second", checkConstraint ? "new-event" : "event"));

        Assert.Equal(19, error.SqliteErrorCode);
        Assert.Equal(1, await CountReceiptsAsync(connection, transaction));
    }

    /// <summary>
    /// 验证缺列等表结构错误正常传播。
    /// </summary>
    [Fact]
    public async Task TryRecordAsync_WhenSchemaInvalid_ShouldPropagateStorageError()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE bing_event_consumption (Consumer TEXT, EventId TEXT, PRIMARY KEY (Consumer, EventId))";
            await create.ExecuteNonQueryAsync();
        }
        using var transaction = connection.BeginTransaction();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        await Assert.ThrowsAsync<SqliteException>(() => store.TryRecordAsync(transaction, "consumer", "event"));
    }

    /// <summary>
    /// 验证收据清理按消费者、截止时间和批量上限删除。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_ShouldDeleteOnlyExpiredConsumerReceiptsAndRespectBatch()
    {
        using var connection = await CreateConnectionAsync();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        var now = DateTime.UtcNow;
        using (var seed = connection.BeginTransaction())
        {
            (await store.TryRecordAsync(seed, "inventory", "old-1")).ShouldBeTrue();
            (await store.TryRecordAsync(seed, "inventory", "old-2")).ShouldBeTrue();
            (await store.TryRecordAsync(seed, "inventory", "new")).ShouldBeTrue();
            (await store.TryRecordAsync(seed, "audit", "old")).ShouldBeTrue();
            await UpdateProcessedAtAsync(connection, seed, "inventory", "old-1", now.AddHours(-3));
            await UpdateProcessedAtAsync(connection, seed, "inventory", "old-2", now.AddHours(-2));
            await UpdateProcessedAtAsync(connection, seed, "audit", "old", now.AddHours(-3));
            seed.Commit();
        }

        using (var transaction = connection.BeginTransaction())
        {
            var deleted = await store.DeleteBeforeAsync(transaction, "inventory", now.AddHours(-1), 1);
            deleted.ShouldBe(1);
            (await CountReceiptsForConsumerAsync(connection, "inventory", transaction)).ShouldBe(2);
            (await CountReceiptsForConsumerAsync(connection, "audit", transaction)).ShouldBe(1);
            transaction.Commit();
        }

        (await CountReceiptsAsync(connection)).ShouldBe(3);
    }

    /// <summary>
    /// 验证收据清理回滚后保留原记录。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenTransactionRollsBack_ShouldKeepReceipts()
    {
        using var connection = await CreateConnectionAsync();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        var now = DateTime.UtcNow;
        using (var seed = connection.BeginTransaction())
        {
            (await store.TryRecordAsync(seed, "inventory", "old")).ShouldBeTrue();
            await UpdateProcessedAtAsync(connection, seed, "inventory", "old", now.AddHours(-2));
            seed.Commit();
        }

        using (var transaction = connection.BeginTransaction())
        {
            (await store.DeleteBeforeAsync(transaction, "inventory", now.AddHours(-1), 10)).ShouldBe(1);
            transaction.Rollback();
        }

        (await CountReceiptsAsync(connection)).ShouldBe(1);
    }

    /// <summary>
    /// 验证取消和非法清理参数不会删除收据。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenCanceledOrInvalid_ShouldNotDeleteReceipts()
    {
        using var connection = await CreateConnectionAsync();
        var store = new EventConsumptionStore(EventConsumptionDatabase.Sqlite);
        var now = DateTime.UtcNow;
        using (var seed = connection.BeginTransaction())
        {
            (await store.TryRecordAsync(seed, "inventory", "old")).ShouldBeTrue();
            await UpdateProcessedAtAsync(connection, seed, "inventory", "old", now.AddHours(-2));
            seed.Commit();
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using (var transaction = connection.BeginTransaction())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteBeforeAsync(transaction,
                "inventory", now.AddHours(-1), 10, cancellation.Token));
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteBeforeAsync(transaction,
                " inventory", now.AddHours(-1), 10));
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteBeforeAsync(transaction,
                "inventory ", now.AddHours(-1), 10));
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteBeforeAsync(transaction,
                "inventory", DateTime.SpecifyKind(now, DateTimeKind.Unspecified), 10));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.DeleteBeforeAsync(transaction,
                "inventory", now.AddHours(-1), 0));
            transaction.Rollback();
        }

        (await CountReceiptsAsync(connection)).ShouldBe(1);
    }

    /// <summary>
    /// 建立隔离的内存数据库。
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        foreach (var statement in EventConsumptionSchema.GetCreateStatements(EventConsumptionDatabase.Sqlite))
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }
        using var business = connection.CreateCommand();
        business.CommandText = "CREATE TABLE business_changes (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL)";
        await business.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// 在消费事务中写入业务记录。
    /// </summary>
    private static async Task InsertBusinessRowAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO business_changes (Id, Value) VALUES (1, 'applied')";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 查询消费记录数量。
    /// </summary>
    private static async Task<long> CountReceiptsAsync(SqliteConnection connection,
        SqliteTransaction transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM bing_event_consumption";
        return (long)await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// 查询指定消费者的收据数量。
    /// </summary>
    private static async Task<long> CountReceiptsForConsumerAsync(SqliteConnection connection, string consumer,
        SqliteTransaction transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM bing_event_consumption WHERE Consumer = @consumer";
        command.Parameters.Add(new SqliteParameter("@consumer", consumer));
        return (long)await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// 修改测试收据的处理时间。
    /// </summary>
    private static async Task UpdateProcessedAtAsync(SqliteConnection connection, SqliteTransaction transaction,
        string consumer, string eventId, DateTime processedAtUtc)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE bing_event_consumption SET ProcessedAtUtc = @processedAtUtc " +
                              "WHERE Consumer = @consumer AND EventId = @eventId";
        command.Parameters.Add(new SqliteParameter("@processedAtUtc",
            processedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
        command.Parameters.Add(new SqliteParameter("@consumer", consumer));
        command.Parameters.Add(new SqliteParameter("@eventId", eventId));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 查询业务记录数量。
    /// </summary>
    private static async Task<long> CountBusinessRowsAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM business_changes";
        return (long)await command.ExecuteScalarAsync();
    }
}
