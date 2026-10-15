using Bing.Data.Events.Ordering;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序事件的 SQLite 事务和持久化语义。
/// </summary>
public sealed class DbOrderedEventStoreTest
{
    /// <summary>
    /// 验证乱序到达、重复投递及连续处理。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenArrivingOutOfOrder_ShouldProcessOnceInSequence()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "item");
        await InTransactionAsync(connection, tx => store.EnqueueAsync(tx, "consumer", stream,
            new OrderedEventMessage("event-3", 3, "value-3")));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Gap,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
        {
            foreach (var sequence in new[] { 1, 2, 1 })
                await store.EnqueueAsync(tx, "consumer", stream,
                    new OrderedEventMessage($"event-{sequence}", sequence, $"value-{sequence}"));
        });

        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(OrderedEventProcessingResult.Empty,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
            var state = await store.GetStateAsync(tx, "consumer", stream);
            Assert.Equal(3, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
        });
        Assert.Equal(new[] { "event-1", "event-2", "event-3" }, await ReadBusinessEventsAsync(connection));
    }

    /// <summary>
    /// 验证存储层拒绝首尾带空白的逻辑消费者。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenConsumerHasBoundaryWhitespace_ShouldRejectBeforeWriting()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "identity");

        await Assert.ThrowsAsync<ArgumentException>(() => InTransactionAsync(connection, transaction =>
            store.EnqueueAsync(transaction, "consumer ", stream,
                new OrderedEventMessage("event-whitespace", 1, "payload"))));

        await InTransactionAsync(connection, async transaction =>
        {
            var state = await store.GetStateAsync(transaction, "consumer", stream);
            Assert.Equal(0, state.LastSequence);
            Assert.Equal(0, state.PendingCount);
        });
    }

    /// <summary>
    /// 验证业务写入和游标回滚后可以重试。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenTransactionRollsBack_ShouldRetryBusinessAndCursor()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "item");
        await InTransactionAsync(connection, tx => store.EnqueueAsync(tx, "consumer", stream,
            new OrderedEventMessage("event-1", 1, "payload")));

        using (var transaction = connection.BeginTransaction())
        {
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(transaction, "consumer", stream, WriteBusinessAsync));
            transaction.Rollback();
        }
        Assert.Empty(await ReadBusinessEventsAsync(connection));
        await InTransactionAsync(connection, async tx =>
        {
            var state = await store.GetStateAsync(tx, "consumer", stream);
            Assert.Equal(0, state.LastSequence);
            Assert.Equal(1, state.PendingCount);
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
        });
        Assert.Equal(new[] { "event-1" }, await ReadBusinessEventsAsync(connection));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(1, (await store.GetStateAsync(tx, "consumer", stream)).LastSequence));
    }

    /// <summary>
    /// 验证失败前序阻塞当前实体而其他实体仍可处理。
    /// </summary>
    [Fact]
    public async Task ProcessNextAsync_WhenPredecessorFails_ShouldRequireRetryOnlyForThatStream()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var blocked = Stream("tenant", "blocked");
        var healthy = Stream("tenant", "healthy");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", blocked, new OrderedEventMessage("b-1", 1, "one"));
            await store.EnqueueAsync(tx, "consumer", blocked, new OrderedEventMessage("b-2", 2, "two"));
            await store.EnqueueAsync(tx, "consumer", healthy, new OrderedEventMessage("h-1", 1, "ok"));
        });
        using (var transaction = connection.BeginTransaction())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ProcessNextAsync(transaction, "consumer", blocked,
                    (_, _, _) => throw new InvalidOperationException("handler failed")));
            transaction.Rollback();
        }
        await InTransactionAsync(connection, tx => store.RecordFailureAsync(tx, "consumer", blocked, 1, "HANDLER_ERROR"));

        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(OrderedEventProcessingResult.Failed,
                await store.ProcessNextAsync(tx, "consumer", blocked, WriteBusinessAsync));
            var state = await store.GetStateAsync(tx, "consumer", blocked);
            Assert.True(state.Failed);
            Assert.Equal(1, state.Attempts);
            Assert.Equal("HANDLER_ERROR", state.LastError);
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", healthy, WriteBusinessAsync));
        });
        await InTransactionAsync(connection, tx => store.RetryAsync(tx, "consumer", blocked));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", blocked, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", blocked, WriteBusinessAsync)));
        Assert.Equal(new[] { "h-1", "b-1", "b-2" }, await ReadBusinessEventsAsync(connection));
    }

    /// <summary>
    /// 验证过期失败报告不会阻断下一事件。
    /// </summary>
    [Fact]
    public async Task RecordFailureAsync_WhenSequenceAlreadyProcessed_ShouldNotFailSuccessor()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "item");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream, new OrderedEventMessage("first", 1, "one"));
            await store.EnqueueAsync(tx, "consumer", stream, new OrderedEventMessage("second", 2, "two"));
        });
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));

        await InTransactionAsync(connection, async tx =>
        {
            await store.RecordFailureAsync(tx, "consumer", stream, 1, "STALE_FAILURE");
            var state = await store.GetStateAsync(tx, "consumer", stream);
            Assert.Equal(1, state.LastSequence);
            Assert.Equal(2, state.FirstPendingSequence);
            Assert.False(state.Failed);
            Assert.Equal(0, state.Attempts);
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
        });
        Assert.Equal(new[] { "first", "second" }, await ReadBusinessEventsAsync(connection));
    }

    /// <summary>
    /// 验证序号分配与事务共同回滚。
    /// </summary>
    [Fact]
    public async Task AllocateSequenceAsync_WhenRolledBack_ShouldNotLeaveGap()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "item");
        using (var transaction = connection.BeginTransaction())
        {
            Assert.Equal(1, await store.AllocateSequenceAsync(transaction, stream));
            transaction.Rollback();
        }
        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(1, await store.AllocateSequenceAsync(tx, stream));
            Assert.Equal(2, await store.AllocateSequenceAsync(tx, stream));
        });
        await InTransactionAsync(connection, async tx =>
            Assert.Equal(3, await store.AllocateSequenceAsync(tx, stream)));
    }

    /// <summary>
    /// 验证租户、消费者及含分隔符的业务流键互不混淆。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenKeysDiffer_ShouldKeepStreamsAndConsumersIsolated()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var left = new OrderedEventStream("a:b", "c", "d");
        var right = new OrderedEventStream("a", "b:c", "d");
        var otherTenant = new OrderedEventStream("other", "c", "d");
        Assert.NotEqual(store.GetStreamKey(left), store.GetStreamKey(right));
        Assert.NotEqual(store.GetStreamKey(left), store.GetStreamKey(otherTenant));
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer-1", left, new OrderedEventMessage("shared", 1, "left"));
            await store.EnqueueAsync(tx, "consumer-1", right, new OrderedEventMessage("right", 1, "right"));
            await store.EnqueueAsync(tx, "consumer-1", otherTenant, new OrderedEventMessage("tenant", 1, "tenant"));
            await store.EnqueueAsync(tx, "consumer-2", left, new OrderedEventMessage("shared", 1, "left"));
        });
        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(3, (await store.GetPendingStreamsAsync(tx, "consumer-1", 10)).Count);
            Assert.Single(await store.GetPendingStreamsAsync(tx, "consumer-2", 10));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer-2", left, WriteBusinessAsync));
            Assert.Equal(0, (await store.GetStateAsync(tx, "consumer-1", left)).LastSequence);
        });
    }

    /// <summary>
    /// 验证统计接口返回消费者的全局待处理事件数，而不是当前分页流数。
    /// </summary>
    [Fact]
    public async Task GetPendingEventCountAsync_ShouldCountAllUnprocessedEvents()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var first = Stream("tenant", "first");
        var second = Stream("tenant", "second");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", first, new OrderedEventMessage("first-1", 1, "one"));
            await store.EnqueueAsync(tx, "consumer", first, new OrderedEventMessage("first-2", 2, "two"));
            await store.EnqueueAsync(tx, "consumer", second, new OrderedEventMessage("second-1", 1, "one"));
        });

        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(3, await store.GetPendingEventCountAsync(tx, "consumer"));
            Assert.Equal(2, await store.GetPendingStreamCountAsync(tx, "consumer"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", first, WriteBusinessAsync));
            Assert.Equal(2, await store.GetPendingEventCountAsync(tx, "consumer"));
            Assert.Equal(2, await store.GetPendingStreamCountAsync(tx, "consumer"));
        });

        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", first, WriteBusinessAsync));
            Assert.Equal(1, await store.GetPendingStreamCountAsync(tx, "consumer"));
        });
    }

    /// <summary>
    /// 统计所有业务流中最老待处理事件的入箱时间。
    /// </summary>
    [Fact]
    public async Task GetOldestPendingEventUtcAsync_ShouldReturnOldestPendingRecord()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "age");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("first", 1, "one"));
            await store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("second", 2, "two"));
        });

        await InTransactionAsync(connection, async tx =>
        {
            var oldest = await store.GetOldestPendingEventUtcAsync(tx, "consumer");
            Assert.NotNull(oldest);
            Assert.True(oldest.Value <= DateTime.UtcNow);
            Assert.Equal(2, await store.GetPendingEventCountAsync(tx, "consumer"));
        });

        await InTransactionAsync(connection, async tx =>
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync)));
        await InTransactionAsync(connection, async tx =>
        {
            var oldest = await store.GetOldestPendingEventUtcAsync(tx, "consumer");
            Assert.NotNull(oldest);
            Assert.True(oldest.Value <= DateTime.UtcNow);
            Assert.Equal(1, await store.GetPendingEventCountAsync(tx, "consumer"));
        });
    }

    /// <summary>
    /// 验证留存清理只删除已完成的旧收件记录。
    /// </summary>
    [Fact]
    public async Task DeleteProcessedBeforeAsync_ShouldKeepPendingAndFailedRecords()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var processed = Stream("tenant", "processed");
        var blocked = Stream("tenant", "blocked");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", processed,
                new OrderedEventMessage("processed-1", 1, "done"));
            await store.EnqueueAsync(tx, "consumer", blocked,
                new OrderedEventMessage("blocked-1", 1, "failed"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", processed, WriteBusinessAsync));
            await store.RecordFailureAsync(tx, "consumer", blocked, 1, "HANDLER_ERROR");
        });

        await InTransactionAsync(connection, async tx =>
        {
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox SET CreatedAtUtc = @created";
            update.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@created",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();

            var deleted = await store.DeleteProcessedBeforeAsync(tx, "consumer", DateTime.UtcNow.AddHours(-1), 10);
            Assert.Equal(1, deleted);
            Assert.Equal(1, await store.GetPendingEventCountAsync(tx, "consumer"));
            Assert.True((await store.GetStateAsync(tx, "consumer", blocked)).Failed);
        });
        Assert.Equal(1, await CountInboxAsync(connection));
        Assert.Equal(1, await CountArchiveAsync(connection));

        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", processed,
                new OrderedEventMessage("processed-1", 1, "done"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", processed,
                new OrderedEventMessage("processed-1", 2, "reused")));
        });
    }

    /// <summary>
    /// 验证历史清理按消费者、时间和批量限制删除，并不触碰收件箱。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_ShouldDeleteOnlyExpiredHistory()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "archived");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("archived-1", 1, "done"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox SET CreatedAtUtc = @created";
            update.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@created",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
            Assert.Equal(1, await store.DeleteProcessedBeforeAsync(tx, "consumer", DateTime.UtcNow.AddHours(-1), 10));
        });

        using (var update = connection.CreateCommand())
        {
            update.CommandText = "UPDATE bing_event_inbox_archive SET ArchivedAtUtc = @archived";
            update.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@archived",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
        }

        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(1, await store.DeleteArchivedBeforeAsync(tx, "consumer", DateTime.UtcNow.AddHours(-1), 1));
            Assert.Equal(0, await store.DeleteArchivedBeforeAsync(tx, "consumer", DateTime.UtcNow.AddHours(-1), 1));
            Assert.Equal(0, await CountArchiveAsync(connection));
            Assert.Equal(0, await store.GetPendingEventCountAsync(tx, "consumer"));
        });
    }

    /// <summary>
    /// 验证历史清理事务回滚后仍保留历史记录。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenTransactionRollsBack_ShouldKeepHistory()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "rollback-archive");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("rollback-archive-1", 1, "done"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox SET CreatedAtUtc = @created";
            update.Parameters.Add(new SqliteParameter("@created",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
            Assert.Equal(1, await store.DeleteProcessedBeforeAsync(tx, "consumer",
                DateTime.UtcNow.AddHours(-1), 10));
        });
        await InTransactionAsync(connection, async tx =>
        {
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox_archive SET ArchivedAtUtc = @archived";
            update.Parameters.Add(new SqliteParameter("@archived",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
        });

        using (var transaction = connection.BeginTransaction())
        {
            Assert.Equal(1, await store.DeleteArchivedBeforeAsync(transaction, "consumer",
                DateTime.UtcNow.AddHours(-1), 10));
            transaction.Rollback();
        }

        Assert.Equal(1, await CountArchiveAsync(connection));
    }

    /// <summary>
    /// 验证历史清理收到已取消令牌时不会删除记录。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenCanceled_ShouldKeepHistory()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var stream = Stream("tenant", "canceled-archive");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("canceled-archive-1", 1, "done"));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", stream, WriteBusinessAsync));
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox SET CreatedAtUtc = @created";
            update.Parameters.Add(new SqliteParameter("@created",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
            Assert.Equal(1, await store.DeleteProcessedBeforeAsync(tx, "consumer",
                DateTime.UtcNow.AddHours(-1), 10));
        });
        await InTransactionAsync(connection, async tx =>
        {
            using var update = connection.CreateCommand();
            update.Transaction = tx;
            update.CommandText = "UPDATE bing_event_inbox_archive SET ArchivedAtUtc = @archived";
            update.Parameters.Add(new SqliteParameter("@archived",
                DateTime.UtcNow.AddHours(-2).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
            await update.ExecuteNonQueryAsync();
        });

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using (var transaction = connection.BeginTransaction())
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteArchivedBeforeAsync(transaction,
                "consumer", DateTime.UtcNow.AddHours(-1), 10, cancellation.Token));
            transaction.Rollback();
        }

        Assert.Equal(1, await CountArchiveAsync(connection));
    }

    /// <summary>
    /// 验证冲突内容、待处理容量、UTF-8 字节上限与取消均不会写入。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenInvalidOrCanceled_ShouldRejectWithoutNewMessage()
    {
        using var connection = await CreateConnectionAsync();
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPayloadBytes: 4,
            maxPendingPerStream: 2);
        var stream = Stream("tenant", "item");
        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", stream, new OrderedEventMessage("first", 1, "一"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("first", 1, "二")));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("other", 1, "一")));
            await Assert.ThrowsAsync<ArgumentException>(() => store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("oversized", 2, "中文")));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("canceled", 2, "ok"), cancellation.Token));
            await store.EnqueueAsync(tx, "consumer", stream, new OrderedEventMessage("second", 2, "ok"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", stream,
                new OrderedEventMessage("third", 3, "ok")));
            Assert.Equal(2, (await store.GetStateAsync(tx, "consumer", stream)).PendingCount);
        });
        Assert.Equal(2, await CountInboxAsync(connection));
    }

    /// <summary>
    /// 验证全局容量按消费者跨业务流限制，并在成功消费后释放容量。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenConsumerCapacityIsFull_ShouldRejectAcrossStreamsAndReleaseAfterProcessing()
    {
        using var connection = await CreateConnectionAsync();
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPendingPerConsumer: 2);
        var first = Stream("tenant", "capacity-first");
        var second = Stream("tenant", "capacity-second");
        var third = Stream("tenant", "capacity-third");

        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", first, new OrderedEventMessage("first", 1, "one"));
            await store.EnqueueAsync(tx, "consumer", second, new OrderedEventMessage("second", 1, "two"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", third,
                new OrderedEventMessage("third", 1, "three")));
        });

        await InTransactionAsync(connection, async tx =>
        {
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await store.ProcessNextAsync(tx, "consumer", first, WriteBusinessAsync));
        });

        await InTransactionAsync(connection, async tx =>
        {
            await store.EnqueueAsync(tx, "consumer", third, new OrderedEventMessage("third", 1, "three"));
            Assert.Equal(2, await store.GetPendingEventCountAsync(tx, "consumer"));
        });
    }

    /// <summary>
    /// 验证全局容量初始化时会纳入旧收件箱中的待处理记录。
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_WhenCapacityIsEnabledAfterExistingMessages_ShouldReconcilePendingCount()
    {
        using var connection = await CreateConnectionAsync();
        var legacy = new DbOrderedEventStore(OrderedEventDatabase.Sqlite);
        var store = new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPendingPerConsumer: 1);
        var first = Stream("tenant", "capacity-existing");
        var second = Stream("tenant", "capacity-new");

        await InTransactionAsync(connection, tx => legacy.EnqueueAsync(tx, "consumer", first,
            new OrderedEventMessage("existing", 1, "one")));

        await InTransactionAsync(connection, async tx =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.EnqueueAsync(tx, "consumer", second,
                new OrderedEventMessage("new", 1, "two")));
        });

        Assert.Equal(1, await CountInboxAsync(connection));
    }

    /// <summary>
    /// 验证全局容量配置必须为正数。
    /// </summary>
    [Fact]
    public void Constructor_WhenConsumerCapacityIsInvalid_ShouldReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPendingPerConsumer: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPendingPerConsumer: -1));
    }

    /// <summary>
    /// 验证旧三参数构造签名和容量专用重载均可使用。
    /// </summary>
    [Fact]
    public void Constructor_ShouldKeepLegacySignatureAndSupportCapacityOverload()
    {
        var legacy = new DbOrderedEventStore(OrderedEventDatabase.Sqlite, 1024, 10);
        var capacity = new DbOrderedEventStore(OrderedEventDatabase.Sqlite, maxPendingPerConsumer: 2);

        Assert.NotNull(legacy);
        Assert.NotNull(capacity);
    }

    /// <summary>
    /// 验证分页可绕过失败实体且重建存储后仍可读取状态。
    /// </summary>
    [Fact]
    public async Task GetPendingStreamsAsync_WhenFirstStreamFails_ShouldPageToOtherStreams()
    {
        using var connection = await CreateConnectionAsync();
        var store = CreateStore();
        var streams = Enumerable.Range(0, 4).Select(index => Stream("tenant", $"item-{index}")).ToArray();
        var ordered = streams.OrderBy(store.GetStreamKey, StringComparer.Ordinal).ToArray();
        await InTransactionAsync(connection, async tx =>
        {
            foreach (var stream in streams)
                await store.EnqueueAsync(tx, "consumer", stream,
                    new OrderedEventMessage($"event-{stream.EntityKey}", 1, "payload"));
            await store.RecordFailureAsync(tx, "consumer", ordered[0], 1, "FAILED");
        });
        var recreated = CreateStore();
        await InTransactionAsync(connection, async tx =>
        {
            var first = await recreated.GetPendingStreamsAsync(tx, "consumer", 1);
            Assert.Equal(ordered[0].EntityKey, Assert.Single(first).EntityKey);
            Assert.True((await recreated.GetStateAsync(tx, "consumer", ordered[0])).Failed);
            var remaining = await recreated.GetPendingStreamsAsync(tx, "consumer", 10,
                recreated.GetStreamKey(ordered[0]));
            Assert.Equal(ordered.Skip(1).Select(item => item.EntityKey),
                remaining.Select(item => item.EntityKey));
            Assert.Equal(OrderedEventProcessingResult.Processed,
                await recreated.ProcessNextAsync(tx, "consumer", remaining[0], WriteBusinessAsync));
        });
        Assert.Single(await ReadBusinessEventsAsync(connection));
    }

    /// <summary>
    /// 创建测试存储。
    /// </summary>
    private static DbOrderedEventStore CreateStore() => new(OrderedEventDatabase.Sqlite);

    /// <summary>
    /// 创建测试业务流。
    /// </summary>
    private static OrderedEventStream Stream(string tenant, string entity) => new(tenant, "inventory", entity);

    /// <summary>
    /// 创建 SQLite 架构和业务记录表。
    /// </summary>
    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        foreach (var statement in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.Sqlite))
        {
            using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }
        using var business = connection.CreateCommand();
        business.CommandText = "CREATE TABLE business_events (EventId TEXT NOT NULL PRIMARY KEY, Sequence INTEGER NOT NULL)";
        await business.ExecuteNonQueryAsync();
        return connection;
    }

    /// <summary>
    /// 在调用方拥有的事务内运行并提交测试动作。
    /// </summary>
    private static async Task InTransactionAsync(SqliteConnection connection, Func<SqliteTransaction, Task> action)
    {
        using var transaction = connection.BeginTransaction();
        await action(transaction);
        transaction.Commit();
    }

    /// <summary>
    /// 使用存储传入的事务写入业务记录。
    /// </summary>
    private static async Task WriteBusinessAsync(OrderedEventMessage message, System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO business_events (EventId, Sequence) VALUES (@id, @sequence)";
        command.Parameters.Add(new SqliteParameter("@id", message.EventId));
        command.Parameters.Add(new SqliteParameter("@sequence", message.Sequence));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取已提交业务事件。
    /// </summary>
    private static async Task<string[]> ReadBusinessEventsAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EventId FROM business_events ORDER BY rowid";
        using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result.ToArray();
    }

    /// <summary>
    /// 读取收件记录数量。
    /// </summary>
    private static async Task<long> CountInboxAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bing_event_inbox";
        return (long)await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// 读取已处理收件历史数量。
    /// </summary>
    private static async Task<long> CountArchiveAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bing_event_inbox_archive";
        return (long)await command.ExecuteScalarAsync();
    }
}
