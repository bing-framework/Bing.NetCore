using System.Data.Common;
using Bing.Data.Events.Ordering;
using Microsoft.Data.Sqlite;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 为有序事件入口测试提供跨连接共享的 SQLite 数据库。
/// </summary>
internal sealed class OrderingSqliteFixture : IDisposable
{
    /// <summary>
    /// 维持共享内存数据库存活的连接。
    /// </summary>
    private readonly SqliteConnection _anchor;

    /// <summary>
    /// 测试数据库连接字符串。
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// 初始化一个 <see cref="OrderingSqliteFixture" /> 类型的实例。
    /// </summary>
    internal OrderingSqliteFixture()
    {
        _connectionString = $"Data Source=bing-ordering-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=5";
        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();
        foreach (var statement in OrderedEventSchema.GetCreateStatements(OrderedEventDatabase.Sqlite))
        {
            using var command = _anchor.CreateCommand();
            command.CommandText = statement;
            command.ExecuteNonQuery();
        }
        using var business = _anchor.CreateCommand();
        business.CommandText = "CREATE TABLE business_events (EventId TEXT NOT NULL PRIMARY KEY, StreamKey TEXT NOT NULL)";
        business.ExecuteNonQuery();
    }

    /// <summary>
    /// 创建生产存储。
    /// </summary>
    internal DbOrderedEventStore CreateStore() => new(OrderedEventDatabase.Sqlite);

    /// <summary>
    /// 创建事务工厂。
    /// </summary>
    internal IOrderedEventTransactionFactory CreateFactory() => new SqliteTransactionFactory(_connectionString);

    /// <summary>
    /// 创建独立业务流。
    /// </summary>
    internal static OrderedEventStream Stream(string entity = "sku") => new("tenant", "inventory", entity);

    /// <summary>
    /// 在真实事务中提交测试动作。
    /// </summary>
    internal async Task InTransactionAsync(Func<DbTransaction, Task> action)
    {
        await using var transaction = await CreateFactory().BeginAsync();
        await action(transaction.Transaction);
        await transaction.CommitAsync();
    }

    /// <summary>
    /// 使用处理回调收到的事务写入业务记录。
    /// </summary>
    internal static async Task WriteBusinessAsync(OrderedEventMessage message, DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO business_events (EventId, StreamKey) VALUES (@id, @stream)";
        var id = command.CreateParameter();
        id.ParameterName = "@id";
        id.Value = message.EventId;
        command.Parameters.Add(id);
        var stream = command.CreateParameter();
        stream.ParameterName = "@stream";
        stream.Value = message.Payload;
        command.Parameters.Add(stream);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 读取已提交业务事件标识。
    /// </summary>
    internal async Task<string[]> ReadBusinessIdsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EventId FROM business_events ORDER BY rowid";
        using var reader = await command.ExecuteReaderAsync();
        var ids = new List<string>();
        while (await reader.ReadAsync())
            ids.Add(reader.GetString(0));
        return ids.ToArray();
    }

    /// <summary>
    /// 读取已提交的发布序号。
    /// </summary>
    internal async Task<long> ReadSequenceAsync(OrderedEventStream stream)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT LastSequence FROM bing_event_sequence WHERE StreamKey = @stream";
        command.Parameters.AddWithValue("@stream", CreateStore().GetStreamKey(stream));
        var value = await command.ExecuteScalarAsync();
        return value == null ? 0 : Convert.ToInt64(value);
    }

    /// <summary>
    /// 调整收件记录时间以验证留存清理。
    /// </summary>
    internal async Task SetInboxCreatedAtAsync(DateTime value)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE bing_event_inbox SET CreatedAtUtc = @created";
        command.Parameters.AddWithValue("@created", value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 调整已处理历史记录时间以验证历史留存清理。
    /// </summary>
    internal async Task SetArchiveArchivedAtAsync(DateTime value)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE bing_event_inbox_archive SET ArchivedAtUtc = @archived";
        command.Parameters.AddWithValue("@archived", value.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 读取收件箱记录数量。
    /// </summary>
    internal async Task<long> CountInboxAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bing_event_inbox";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 读取已处理历史记录数量。
    /// </summary>
    internal async Task<long> CountArchiveAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM bing_event_inbox_archive";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 释放锚连接。
    /// </summary>
    public void Dispose() => _anchor.Dispose();

    /// <summary>
    /// 为每次调用创建独立 SQLite 事务。
    /// </summary>
    private sealed class SqliteTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <summary>
        /// 连接字符串。
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// 初始化一个 <see cref="SqliteTransactionFactory" /> 类型的实例。
        /// </summary>
        internal SqliteTransactionFactory(string connectionString) => _connectionString = connectionString;

        /// <inheritdoc />
        public async Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
                return new SqliteTransactionScope(connection, transaction);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// 拥有一次 SQLite 连接和事务。
    /// </summary>
    private sealed class SqliteTransactionScope : IOrderedEventTransaction
    {
        /// <summary>
        /// 本次连接。
        /// </summary>
        private readonly SqliteConnection _connection;

        /// <summary>
        /// 本次事务。
        /// </summary>
        private readonly SqliteTransaction _transaction;

        /// <summary>
        /// 事务是否已结束。
        /// </summary>
        private bool _finished;

        /// <summary>
        /// 初始化一个 <see cref="SqliteTransactionScope" /> 类型的实例。
        /// </summary>
        internal SqliteTransactionScope(SqliteConnection connection, SqliteTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        /// <inheritdoc />
        public DbTransaction Transaction => _transaction;

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _finished = true;
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_finished)
                return;
            await _transaction.RollbackAsync(cancellationToken);
            _finished = true;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_finished)
                    await RollbackAsync();
                await _transaction.DisposeAsync();
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
