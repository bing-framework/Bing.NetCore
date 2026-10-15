using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 通过数据库事务保存有序事件并串行推进实体游标。
/// </summary>
public sealed class DbOrderedEventStore : IOrderedEventStore, IOrderedEventStoreStatistics,
    IOrderedEventStorePendingStreamStatistics, IOrderedEventStorePendingAgeStatistics,
    IOrderedEventStoreRetention, IOrderedEventStoreArchiveRetention
{
    /// <summary>
    /// 数据库语法。
    /// </summary>
    private readonly OrderedEventDatabase _database;
    /// <summary>
    /// 单条事件内容的 UTF-8 字节上限。
    /// </summary>
    private readonly int _maxPayloadBytes;
    /// <summary>
    /// 单消费者实体的待处理数量上限。
    /// </summary>
    private readonly int _maxPendingPerStream;
    /// <summary>
    /// 单消费者跨业务流的待处理数量上限。
    /// </summary>
    private readonly int? _maxPendingPerConsumer;
    /// <summary>
    /// 拒绝无效代理项的 UTF-8 编码器。
    /// </summary>
    private readonly UTF8Encoding _encoding = new(false, true);

    /// <summary>
    /// 初始化一个 <see cref="DbOrderedEventStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <param name="maxPayloadBytes">单条事件内容字节上限。</param>
    /// <param name="maxPendingPerStream">单消费者实体的待处理数量上限。</param>
    public DbOrderedEventStore(OrderedEventDatabase database, int maxPayloadBytes = 65536,
        int maxPendingPerStream = 10000)
        : this(database, maxPayloadBytes, maxPendingPerStream, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="DbOrderedEventStore" /> 类型的实例。
    /// </summary>
    /// <remarks>
    /// 初始化一个启用全局容量闸门的 <see cref="DbOrderedEventStore"/> 实例。
    /// </remarks>
    /// <param name="database">数据库语法。</param>
    /// <param name="maxPendingPerConsumer">单消费者跨业务流的待处理数量上限。</param>
    public DbOrderedEventStore(OrderedEventDatabase database, int? maxPendingPerConsumer)
        : this(database, 65536, 10000, maxPendingPerConsumer)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="DbOrderedEventStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <param name="maxPayloadBytes">单条事件内容字节上限。</param>
    /// <param name="maxPendingPerStream">单消费者实体的待处理数量上限。</param>
    /// <param name="maxPendingPerConsumer">单消费者跨业务流的待处理数量上限；为空时不启用全局容量闸门。</param>
    public DbOrderedEventStore(OrderedEventDatabase database, int maxPayloadBytes,
        int maxPendingPerStream, int? maxPendingPerConsumer)
    {
        if (!Enum.IsDefined(typeof(OrderedEventDatabase), database))
            throw new ArgumentOutOfRangeException(nameof(database));
        if (maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes));
        if (maxPendingPerStream <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingPerStream));
        if (maxPendingPerConsumer.HasValue && maxPendingPerConsumer.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPendingPerConsumer));
        _database = database;
        _maxPayloadBytes = maxPayloadBytes;
        _maxPendingPerStream = maxPendingPerStream;
        _maxPendingPerConsumer = maxPendingPerConsumer;
    }

    /// <inheritdoc />
    public string GetStreamKey(OrderedEventStream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        return Hash(EncodePart(stream.TenantId) + EncodePart(stream.Stream) + EncodePart(stream.EntityKey));
    }

    /// <inheritdoc />
    public async Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
        CancellationToken cancellationToken = default)
    {
        var key = GetStreamKey(stream);
        var insert = "INSERT INTO bing_event_sequence (StreamKey, LastSequence) VALUES (@stream, 0)";
        using (var command = Command(transaction, Upsert(insert, "StreamKey", "LastSequence"), ("@stream", key)))
            await command.ExecuteNonQueryAsync(cancellationToken);
        using (var command = Command(transaction,
                   "UPDATE bing_event_sequence SET LastSequence = LastSequence + 1 " +
                   "WHERE StreamKey = @stream AND LastSequence < 9223372036854775807", ("@stream", key)))
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new OverflowException("业务流序号已耗尽。");
        }
        using var read = Command(transaction, "SELECT LastSequence FROM bing_event_sequence WHERE StreamKey = @stream",
            ("@stream", key));
        return Convert.ToInt64(await read.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        OrderedEventMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (_encoding.GetByteCount(message.Payload) > _maxPayloadBytes)
            throw new ArgumentException("事件内容超过配置的字节上限。", nameof(message));
        var keys = Keys(consumer, stream);
        var pendingForConsumer = await LockConsumerCapacityAsync(transaction, consumer, cancellationToken);
        var eventKey = Hash(message.EventId);
        using (var existing = Command(transaction,
                   CurrentRead("SELECT StreamKey, EventId, Sequence, Payload, ContractVersion FROM (" +
                   "SELECT StreamKey, EventId, Sequence, Payload, ContractVersion FROM bing_event_inbox " +
                   "WHERE ConsumerKey = @consumer AND (EventKey = @event OR (StreamKey = @stream AND Sequence = @sequence)) " +
                   "UNION ALL " +
                   "SELECT StreamKey, EventId, Sequence, Payload, ContractVersion FROM bing_event_inbox_archive " +
                   "WHERE ConsumerKey = @consumer AND (EventKey = @event OR (StreamKey = @stream AND Sequence = @sequence))" +
                   ") existing LIMIT 1"),
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@event", eventKey), ("@sequence", message.Sequence)))
        using (var reader = await existing.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                if (!string.Equals(reader.GetString(0), keys.Stream, StringComparison.Ordinal) ||
                    !string.Equals(reader.GetString(1), message.EventId, StringComparison.Ordinal) ||
                    reader.GetInt64(2) != message.Sequence ||
                    !string.Equals(reader.GetString(3), message.Payload, StringComparison.Ordinal) ||
                    !string.Equals(reader.GetString(4), message.ContractVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException("重复事件的标识、序号或内容不一致。");
                return;
            }
        }
        if (_maxPendingPerConsumer.HasValue && pendingForConsumer >= _maxPendingPerConsumer.Value)
            throw new InvalidOperationException("逻辑消费者待处理事件已达到全局容量上限，需暂停接收并处理积压。");
        var last = await LockCursorAsync(transaction, consumer, stream, cancellationToken);
        if (message.Sequence <= last)
            throw new InvalidOperationException("事件序号已处理，但缺少可验证的重复记录。");
        using (var count = Command(transaction,
                   CurrentRead("SELECT COUNT(*) FROM bing_event_inbox WHERE ConsumerKey = @consumer AND StreamKey = @stream AND Status <> 2"),
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream)))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) >= _maxPendingPerStream)
                throw new InvalidOperationException("业务流待处理事件已达到容量上限，需暂停接收并处理积压。");
        }
        using var insertCommand = Command(transaction,
            "INSERT INTO bing_event_inbox (ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, Status, Attempts, LastError) " +
            "VALUES (@consumer, @stream, @event, @id, @sequence, @payload, @version, @created, 0, 0, '')",
            ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@event", eventKey), ("@id", message.EventId),
            ("@sequence", message.Sequence), ("@payload", message.Payload), ("@version", message.ContractVersion),
            ("@created", CreatedAtUtc()));
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        if (_maxPendingPerConsumer.HasValue)
            await RefreshConsumerCapacityAsync(transaction, keys.Consumer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction, string consumer,
        OrderedEventStream stream, Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        var keys = Keys(consumer, stream);
        await LockConsumerCapacityAsync(transaction, consumer, cancellationToken);
        var last = await LockCursorAsync(transaction, consumer, stream, cancellationToken);
        if (last == long.MaxValue)
            return OrderedEventProcessingResult.Empty;
        OrderedEventMessage message;
        using (var read = Command(transaction,
                   CurrentRead("SELECT EventId, Sequence, Payload, ContractVersion, Status FROM bing_event_inbox " +
                   "WHERE ConsumerKey = @consumer AND StreamKey = @stream AND Status <> 2 ORDER BY Sequence LIMIT 1"),
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream)))
        using (var reader = await read.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return OrderedEventProcessingResult.Empty;
            if (reader.GetInt64(1) != last + 1)
                return OrderedEventProcessingResult.Gap;
            if (reader.GetInt32(4) == 1)
                return OrderedEventProcessingResult.Failed;
            message = new OrderedEventMessage(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3));
        }
        cancellationToken.ThrowIfCancellationRequested();
        await handler(message, transaction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using (var complete = Command(transaction,
                   "UPDATE bing_event_inbox SET Status = 2, LastError = '' " +
                   "WHERE ConsumerKey = @consumer AND StreamKey = @stream AND Sequence = @sequence AND Status = 0",
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@sequence", message.Sequence)))
        {
            if (await complete.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("处理期间事件状态发生变化，必须回滚业务事务。");
        }
        using var advance = Command(transaction,
            "UPDATE bing_event_cursor SET LastSequence = @sequence WHERE ConsumerKey = @consumer AND StreamKey = @stream AND LastSequence = @last",
            ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@sequence", message.Sequence), ("@last", last));
        if (await advance.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("处理期间游标发生变化，必须回滚业务事务。");
        if (_maxPendingPerConsumer.HasValue)
            await RefreshConsumerCapacityAsync(transaction, keys.Consumer, cancellationToken);
        return OrderedEventProcessingResult.Processed;
    }

    /// <inheritdoc />
    public async Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        long failedSequence, string errorCode, CancellationToken cancellationToken = default)
    {
        if (failedSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(failedSequence));
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 200)
            throw new ArgumentException("错误标识不能为空且不能超过 200 个字符。", nameof(errorCode));
        var keys = Keys(consumer, stream);
        await LockConsumerCapacityAsync(transaction, consumer, cancellationToken);
        var last = await LockCursorAsync(transaction, consumer, stream, cancellationToken);
        if (last == long.MaxValue || failedSequence != last + 1)
            return;
        using var command = Command(transaction,
            "UPDATE bing_event_inbox SET Status = 1, Attempts = Attempts + 1, LastError = @error " +
            "WHERE ConsumerKey = @consumer AND StreamKey = @stream AND Sequence = @sequence AND Status = 0",
            ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@sequence", failedSequence), ("@error", errorCode));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default)
    {
        var keys = Keys(consumer, stream);
        await LockConsumerCapacityAsync(transaction, consumer, cancellationToken);
        var last = await LockCursorAsync(transaction, consumer, stream, cancellationToken);
        if (last == long.MaxValue)
            return;
        using var command = Command(transaction,
            "UPDATE bing_event_inbox SET Status = 0 WHERE ConsumerKey = @consumer AND StreamKey = @stream AND Sequence = @sequence AND Status = 1",
            ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@sequence", last + 1));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
        OrderedEventStream stream, CancellationToken cancellationToken = default)
    {
        var keys = Keys(consumer, stream);
        using var command = Command(transaction,
            "SELECT c.LastSequence, " +
            "(SELECT COUNT(*) FROM bing_event_inbox pending WHERE pending.ConsumerKey = c.ConsumerKey " +
            "AND pending.StreamKey = c.StreamKey AND pending.Status <> 2), " +
            "head.Sequence, head.Status, head.Attempts, head.LastError " +
            "FROM bing_event_cursor c LEFT JOIN bing_event_inbox head ON " +
            "head.ConsumerKey = c.ConsumerKey AND head.StreamKey = c.StreamKey AND head.Sequence = " +
            "(SELECT MIN(earliest.Sequence) FROM bing_event_inbox earliest WHERE " +
            "earliest.ConsumerKey = c.ConsumerKey AND earliest.StreamKey = c.StreamKey AND earliest.Status <> 2) " +
            "WHERE c.ConsumerKey = @consumer AND c.StreamKey = @stream",
            ("@consumer", keys.Consumer), ("@stream", keys.Stream));
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new OrderedEventStreamState(stream, 0, null, 0, false, 0, null);
        var last = reader.GetInt64(0);
        var count = reader.GetInt64(1);
        return reader.IsDBNull(2)
            ? new OrderedEventStreamState(stream, last, null, 0, false, 0, null)
            : new OrderedEventStreamState(stream, last, reader.GetInt64(2), count,
                reader.GetInt32(3) == 1, reader.GetInt32(4), reader.GetString(5));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction, string consumer,
        int limit, string afterStreamKey = null, CancellationToken cancellationToken = default)
    {
        var consumerKey = ConsumerKey(consumer);
        if (limit <= 0 || limit > 1000)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (afterStreamKey?.Length > 64)
            throw new ArgumentException("分页键不能超过 64 个字符。", nameof(afterStreamKey));
        using var command = Command(transaction,
            "SELECT c.TenantId, c.StreamName, c.EntityKey FROM bing_event_cursor c " +
            "WHERE c.ConsumerKey = @consumer AND c.StreamKey > @after AND EXISTS " +
            "(SELECT 1 FROM bing_event_inbox i WHERE i.ConsumerKey = c.ConsumerKey AND i.StreamKey = c.StreamKey AND i.Status <> 2) " +
            "ORDER BY c.StreamKey LIMIT @limit",
            ("@consumer", consumerKey), ("@after", afterStreamKey ?? string.Empty), ("@limit", limit));
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var streams = new List<OrderedEventStream>();
        while (await reader.ReadAsync(cancellationToken))
            streams.Add(new OrderedEventStream(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return streams.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<long> GetPendingEventCountAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default)
    {
        var consumerKey = ConsumerKey(consumer);
        using var command = Command(transaction,
            "SELECT COUNT(*) FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status <> 2",
            ("@consumer", consumerKey));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<long> GetPendingStreamCountAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default)
    {
        var consumerKey = ConsumerKey(consumer);
        using var command = Command(transaction,
            "SELECT COUNT(*) FROM bing_event_cursor c WHERE c.ConsumerKey = @consumer AND EXISTS " +
            "(SELECT 1 FROM bing_event_inbox i WHERE i.ConsumerKey = c.ConsumerKey AND i.StreamKey = c.StreamKey AND i.Status <> 2)",
            ("@consumer", consumerKey));
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetOldestPendingEventUtcAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default)
    {
        var consumerKey = ConsumerKey(consumer);
        using var command = Command(transaction,
            "SELECT MIN(CreatedAtUtc) FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status <> 2",
            ("@consumer", consumerKey));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value == null || value == DBNull.Value)
            return null;
        if (value is DateTime dateTime)
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;
        throw new InvalidOperationException("有序事件入箱时间格式无效。");
    }

    /// <inheritdoc />
    public async Task<int> DeleteProcessedBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "清理批量必须位于 1 到 10000 之间。");

        var consumerKey = ConsumerKey(consumer);
        var cutoff = _database == OrderedEventDatabase.MySql
            ? (object)cutoffUtc
            : cutoffUtc.ToString("O", CultureInfo.InvariantCulture);
        var archivedAtUtc = DateTime.UtcNow;
        var archived = _database == OrderedEventDatabase.MySql
            ? (object)archivedAtUtc
            : archivedAtUtc.ToString("O", CultureInfo.InvariantCulture);
        var archiveCommandText = _database == OrderedEventDatabase.MySql
            ? "INSERT INTO bing_event_inbox_archive " +
              "(ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, ArchivedAtUtc) " +
              "SELECT ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, @archived " +
              "FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status = 2 AND CreatedAtUtc < @cutoff " +
              "ORDER BY CreatedAtUtc, StreamKey, Sequence LIMIT @limit " +
              "ON DUPLICATE KEY UPDATE EventKey = EventKey"
            : "INSERT OR IGNORE INTO bing_event_inbox_archive " +
              "(ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, ArchivedAtUtc) " +
              "SELECT ConsumerKey, StreamKey, EventKey, EventId, Sequence, Payload, ContractVersion, CreatedAtUtc, @archived " +
              "FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status = 2 AND CreatedAtUtc < @cutoff " +
              "ORDER BY CreatedAtUtc, StreamKey, Sequence LIMIT @limit";
        using (var archive = Command(transaction, archiveCommandText,
                   ("@consumer", consumerKey), ("@cutoff", cutoff), ("@archived", archived), ("@limit", batchSize)))
            await archive.ExecuteNonQueryAsync(cancellationToken);

        var deleteCommandText = _database == OrderedEventDatabase.MySql
            ? "DELETE FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status = 2 " +
              "AND CreatedAtUtc < @cutoff ORDER BY CreatedAtUtc, StreamKey, Sequence LIMIT @limit"
            : "DELETE FROM bing_event_inbox WHERE rowid IN (SELECT rowid FROM bing_event_inbox " +
              "WHERE ConsumerKey = @consumer AND Status = 2 AND CreatedAtUtc < @cutoff " +
              "ORDER BY CreatedAtUtc, StreamKey, Sequence LIMIT @limit)";
        using var command = Command(transaction, deleteCommandText,
            ("@consumer", consumerKey), ("@cutoff", cutoff), ("@limit", batchSize));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteArchivedBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("历史留存截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "历史清理批量必须位于 1 到 10000 之间。");

        var consumerKey = ConsumerKey(consumer);
        var cutoff = _database == OrderedEventDatabase.MySql
            ? (object)cutoffUtc
            : cutoffUtc.ToString("O", CultureInfo.InvariantCulture);
        var commandText = _database == OrderedEventDatabase.MySql
            ? "DELETE FROM bing_event_inbox_archive WHERE ConsumerKey = @consumer AND ArchivedAtUtc < @cutoff " +
              "ORDER BY ArchivedAtUtc, StreamKey, Sequence LIMIT @limit"
            : "DELETE FROM bing_event_inbox_archive WHERE rowid IN (SELECT rowid FROM bing_event_inbox_archive " +
              "WHERE ConsumerKey = @consumer AND ArchivedAtUtc < @cutoff " +
              "ORDER BY ArchivedAtUtc, StreamKey, Sequence LIMIT @limit)";
        using var command = Command(transaction, commandText,
            ("@consumer", consumerKey), ("@cutoff", cutoff), ("@limit", batchSize));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 创建并锁定实体游标。
    /// </summary>
    private async Task<long> LockCursorAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken)
    {
        var keys = Keys(consumer, stream);
        var insert = "INSERT INTO bing_event_cursor (ConsumerKey, StreamKey, Consumer, TenantId, StreamName, EntityKey, LastSequence) " +
                     "VALUES (@consumer, @stream, @name, @tenant, @flow, @entity, 0)";
        using (var command = Command(transaction, Upsert(insert, "ConsumerKey, StreamKey", "LastSequence"),
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream), ("@name", consumer),
                   ("@tenant", stream.TenantId), ("@flow", stream.Stream), ("@entity", stream.EntityKey)))
            await command.ExecuteNonQueryAsync(cancellationToken);
        // 写锁覆盖本事务后续的收件、业务回调与游标推进。SQLite 和 MySQL 都在事务结束后释放。
        using (var command = Command(transaction,
                   "UPDATE bing_event_cursor SET LastSequence = LastSequence WHERE ConsumerKey = @consumer AND StreamKey = @stream",
                   ("@consumer", keys.Consumer), ("@stream", keys.Stream)))
            await command.ExecuteNonQueryAsync(cancellationToken);
        using var read = Command(transaction,
            CurrentRead("SELECT LastSequence FROM bing_event_cursor WHERE ConsumerKey = @consumer AND StreamKey = @stream"),
            ("@consumer", keys.Consumer), ("@stream", keys.Stream));
        return Convert.ToInt64(await read.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 锁定消费者容量行并返回当前待处理数量。
    /// </summary>
    /// <remarks>容量行按消费者串行化入箱和消费，缺失行会按现有收件箱数据初始化。</remarks>
    private async Task<long> LockConsumerCapacityAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken)
    {
        if (!_maxPendingPerConsumer.HasValue)
            return 0;

        var consumerKey = ConsumerKey(consumer);
        var insert = "INSERT INTO bing_event_consumer_capacity (ConsumerKey, PendingCount) VALUES (@consumer, 0)";
        using (var command = Command(transaction, Upsert(insert, "ConsumerKey", "PendingCount"),
                   ("@consumer", consumerKey)))
            await command.ExecuteNonQueryAsync(cancellationToken);

        using (var lockCommand = Command(transaction,
                   CurrentRead("SELECT PendingCount FROM bing_event_consumer_capacity WHERE ConsumerKey = @consumer"),
                   ("@consumer", consumerKey)))
        {
            if (await lockCommand.ExecuteScalarAsync(cancellationToken) == null)
                throw new InvalidOperationException("有序事件消费者容量行不存在。");
        }

        return await RefreshConsumerCapacityAsync(transaction, consumerKey, cancellationToken);
    }

    /// <summary>
    /// 按当前事务可见收件箱重新计算消费者容量计数。
    /// </summary>
    private async Task<long> RefreshConsumerCapacityAsync(DbTransaction transaction, string consumerKey,
        CancellationToken cancellationToken)
    {
        using var count = Command(transaction,
            "SELECT COUNT(*) FROM bing_event_inbox WHERE ConsumerKey = @consumer AND Status <> 2",
            ("@consumer", consumerKey));
        var pending = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        using var update = Command(transaction,
            "UPDATE bing_event_consumer_capacity SET PendingCount = @pending WHERE ConsumerKey = @consumer",
            ("@pending", pending), ("@consumer", consumerKey));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("有序事件消费者容量计数更新失败。");
        return pending;
    }

    /// <summary>
    /// 生成只处理指定唯一键冲突的插入语句。
    /// </summary>
    private string Upsert(string insert, string conflictColumns, string unchangedColumn) =>
        _database == OrderedEventDatabase.MySql
            ? insert + " ON DUPLICATE KEY UPDATE " + unchangedColumn + " = " + unchangedColumn
            : insert + " ON CONFLICT (" + conflictColumns + ") DO NOTHING";

    /// <summary>
    /// 读取当前锁定版本，避免 MySQL 可重复读快照导致游标回退。
    /// </summary>
    private string CurrentRead(string sql) => _database == OrderedEventDatabase.MySql ? sql + " FOR UPDATE" : sql;

    /// <summary>
    /// 创建调用方事务内的参数化命令。
    /// </summary>
    private static DbCommand Command(DbTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        var connection = transaction.Connection ?? throw new InvalidOperationException("事件事务没有关联数据库连接。");
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var item in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Name;
            parameter.Value = item.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    /// <summary>
    /// 获取消费者和业务流的精确存储键。
    /// </summary>
    private (string Consumer, string Stream) Keys(string consumer, OrderedEventStream stream) =>
        (ConsumerKey(consumer), GetStreamKey(stream));

    /// <summary>
    /// 验证并编码逻辑消费者。
    /// </summary>
    private string ConsumerKey(string consumer)
    {
        if (string.IsNullOrWhiteSpace(consumer) || consumer.Length > 200)
            throw new ArgumentException("逻辑消费者不能为空且不能超过 200 个字符。", nameof(consumer));
        if (!string.Equals(consumer, consumer.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("逻辑消费者不能包含首尾空白。", nameof(consumer));
        return Hash(consumer);
    }

    /// <summary>
    /// 获取当前 UTC 入箱时间的数据库参数值。
    /// </summary>
    private object CreatedAtUtc()
    {
        var value = DateTime.UtcNow;
        return _database == OrderedEventDatabase.MySql
            ? value
            : value.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 生成不受数据库文本排序和尾空格影响的键。
    /// </summary>
    private string Hash(string value)
    {
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(_encoding.GetBytes(value))).Replace("-", string.Empty);
    }

    /// <summary>
    /// 编码业务流组成部分，避免分隔符歧义。
    /// </summary>
    private static string EncodePart(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
}
