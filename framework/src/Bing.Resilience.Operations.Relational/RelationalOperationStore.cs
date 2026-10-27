using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;

namespace Bing.Resilience.Operations.Relational;

public enum ResilienceRelationalDialect { Sqlite, MySql }

/// <summary>业务处理器复用的实际 ADO.NET 事务资源。</summary>
public interface IRelationalOperationTransaction : IOperationTransaction
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
}

/// <summary>显式关系型 Journal。驱动由宿主提供，不自动执行生产迁移。</summary>
public sealed partial class RelationalOperationStore : IResilienceOperationStore, IOperationRetentionStore
{
    private readonly Func<CancellationToken, ValueTask<DbConnection>> _connectionFactory;
    private readonly ResilienceRelationalDialect _dialect;
    private readonly TimeProvider _clock;
    private readonly string _prefix;
    private readonly int _maximumDataBytes;
    public bool IsDurable => true;
    public RelationalOperationStore(Func<CancellationToken, ValueTask<DbConnection>> connectionFactory,
        ResilienceRelationalDialect dialect, string tablePrefix = "bing_resilience", int maximumDataBytes = 1048576, TimeProvider? clock = null)
    {
        if (!Enum.IsDefined(typeof(ResilienceRelationalDialect), dialect) || maximumDataBytes < 1 ||
            string.IsNullOrWhiteSpace(tablePrefix) || tablePrefix.Length > 32 || tablePrefix.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
            throw new ResilienceConfigurationException("关系型方言、表名前缀或内容上限无效。");
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory)); _dialect = dialect; _prefix = tablePrefix; _maximumDataBytes = maximumDataBytes; _clock = clock ?? TimeProvider.System;
    }
    public async ValueTask<IOperationTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        try
        {
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync(_dialect == ResilienceRelationalDialect.Sqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
            return new Session(this, connection, transaction, true);
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }
    /// <summary>加入宿主事务，不取得连接/事务所有权；EF/Dapper 等必须传入实际业务资源。</summary>
    public IOperationTransaction Enlist(DbConnection connection, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(transaction.Connection, connection) || connection.State != ConnectionState.Open)
            throw new ResilienceConfigurationException("Journal 必须复用同一个已打开的业务连接与事务。");
        return new Session(this, connection, transaction, false);
    }
    /// <summary>显式创建独立表；仅由宿主迁移流程或测试调用。</summary>
    public async ValueTask InitializeAsync(CancellationToken token = default)
    {
        await using var connection = await _connectionFactory(token).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(token).ConfigureAwait(false);
        var text = _dialect == ResilienceRelationalDialect.MySql ? "LONGTEXT" : "TEXT";
        var suffix = _dialect == ResilienceRelationalDialect.MySql ? " ENGINE=InnoDB CHARACTER SET utf8mb4 COLLATE utf8mb4_bin" : "";
        var schema = new[]
        {
            $"CREATE TABLE IF NOT EXISTS {_prefix}_operations (id VARCHAR(64) PRIMARY KEY, kind VARCHAR(64) NOT NULL, scope_hash CHAR(64) NOT NULL, key_hash CHAR(64) NOT NULL, fingerprint CHAR(64) NOT NULL, state VARCHAR(64) NOT NULL, version BIGINT NOT NULL, data {text} NOT NULL, attempts INT NOT NULL, created_at BIGINT NOT NULL, updated_at BIGINT NOT NULL, next_at BIGINT NOT NULL, deadline BIGINT NOT NULL, lease_token VARCHAR(64) NULL, lease_until BIGINT NULL, UNIQUE(kind, scope_hash, key_hash)){suffix}",
            $"CREATE TABLE IF NOT EXISTS {_prefix}_attempts (operation_id VARCHAR(64) NOT NULL, attempt_no INT NOT NULL, kind VARCHAR(64) NOT NULL, lease_token VARCHAR(64) NOT NULL, started_at BIGINT NOT NULL, completed_at BIGINT NULL, outcome VARCHAR(64) NULL, generation BIGINT NULL, endpoint VARCHAR(256) NULL, PRIMARY KEY(operation_id, attempt_no)){suffix}",
            $"CREATE TABLE IF NOT EXISTS {_prefix}_audit (operation_id VARCHAR(64) NOT NULL, version BIGINT NOT NULL, event_type VARCHAR(64) NOT NULL, actor VARCHAR(128) NOT NULL, reason {text} NOT NULL, at_utc BIGINT NOT NULL, PRIMARY KEY(operation_id, version, event_type)){suffix}"
        };
        foreach (var sql in schema) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        var createIndex = true;
        if (_dialect == ResilienceRelationalDialect.MySql)
        {
            await using var check = connection.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='{_prefix}_operations' AND index_name='{_prefix}_due'";
            createIndex = Convert.ToInt64(await check.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture) == 0;
        }
        if (createIndex)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE INDEX {(_dialect == ResilienceRelationalDialect.Sqlite ? "IF NOT EXISTS " : "")}{_prefix}_due ON {_prefix}_operations(kind,next_at,id)";
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
    }
    private bool IsDuplicate(DbException error) => _dialect switch
    {
        ResilienceRelationalDialect.MySql => error.GetType().GetProperty("Number")?.GetValue(error) is 1062,
        _ => error.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(error) is 1555 or 2067
    };
    private void Validate(OperationRecord r)
    {
        if (string.IsNullOrWhiteSpace(r.Id) || r.Id.Length > 64 || string.IsNullOrWhiteSpace(r.Kind) || r.Kind.Length > 64 ||
            string.IsNullOrWhiteSpace(r.State) || r.State.Length > 64 || r.Version < 0 || r.Attempts < 0 ||
            new[] { r.ScopeHash, r.KeyHash, r.Fingerprint }.Any(x => x is null || x.Length != 64 || x.Any(c => !Uri.IsHexDigit(c))) ||
            r.Data is null || Encoding.UTF8.GetByteCount(r.Data) > _maximumDataBytes || r.Deadline < r.CreatedAt ||
            r.LeaseToken?.Length > 64)
            throw new ResilienceConfigurationException("持久操作身份、摘要、状态或数据上限无效。");
    }
    private sealed partial class Session(RelationalOperationStore owner, DbConnection connection, DbTransaction transaction, bool owns) : IRelationalOperationTransaction
    {
        private bool _finished, _disposed;
        private string Table => owner._prefix + "_operations";
        public bool OwnsTransaction => owns;
        public DbConnection Connection => connection;
        public DbTransaction Transaction => transaction;
        private DbCommand Command(string sql, params (string Name, object? Value)[] parameters)
        {
            if (_disposed || _finished) throw new ObjectDisposedException(nameof(Session));
            var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter(); parameter.ParameterName = name;
                parameter.Value = value is DateTimeOffset date ? date.ToUnixTimeMilliseconds() : value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            return command;
        }
        private async ValueTask<DateTimeOffset> Now(CancellationToken token)
        {
            if (owner._dialect == ResilienceRelationalDialect.Sqlite) return owner._clock.GetUtcNow();
            await using var command = Command("SELECT CAST(UNIX_TIMESTAMP(CURRENT_TIMESTAMP(3)) * 1000 AS SIGNED)");
            return DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(await command.ExecuteScalarAsync(token).ConfigureAwait(false), CultureInfo.InvariantCulture));
        }
        private static OperationRecord Read(DbDataReader r) => new(
            r.GetString(r.GetOrdinal("id")), r.GetString(r.GetOrdinal("kind")), r.GetString(r.GetOrdinal("scope_hash")),
            r.GetString(r.GetOrdinal("key_hash")), r.GetString(r.GetOrdinal("fingerprint")), r.GetString(r.GetOrdinal("state")),
            Convert.ToInt64(r["version"], CultureInfo.InvariantCulture), r.GetString(r.GetOrdinal("data")), Convert.ToInt32(r["attempts"], CultureInfo.InvariantCulture),
            Date(r["created_at"]), Date(r["updated_at"]), Date(r["next_at"]), Date(r["deadline"]),
            r["lease_token"] is DBNull ? null : (string)r["lease_token"], r["lease_until"] is DBNull ? null : Date(r["lease_until"]));
        private static DateTimeOffset Date(object value) => DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        public async ValueTask<OperationRecord?> FindAsync(string id, CancellationToken token = default)
        {
            await using var command = Command($"SELECT * FROM {Table} WHERE id=@id", ("@id", id));
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await reader.ReadAsync(token).ConfigureAwait(false) ? Read(reader) : null;
        }
        public async ValueTask<OperationRecord?> FindByKeyAsync(string kind, string scopeHash, string keyHash, CancellationToken token = default)
        {
            await using var command = Command($"SELECT * FROM {Table} WHERE kind=@kind AND scope_hash=@scope AND key_hash=@key",
                ("@kind", kind), ("@scope", scopeHash), ("@key", keyHash));
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            return await reader.ReadAsync(token).ConfigureAwait(false) ? Read(reader) : null;
        }
        private static (string, object?)[] Parameters(OperationRecord r) => new (string, object?)[]
        {
            ("@id",r.Id), ("@kind",r.Kind), ("@scope",r.ScopeHash), ("@key",r.KeyHash), ("@fingerprint",r.Fingerprint),
            ("@state",r.State), ("@version",r.Version), ("@data",r.Data), ("@attempts",r.Attempts), ("@created",r.CreatedAt),
            ("@updated",r.UpdatedAt), ("@next",r.NextAttemptAt), ("@deadline",r.Deadline), ("@lease",r.LeaseToken), ("@until",r.LeaseUntil)
        };
        public async ValueTask<bool> TryInsertAsync(OperationRecord record, CancellationToken token = default)
        {
            owner.Validate(record);
            await using var command = Command($"INSERT INTO {Table} (id,kind,scope_hash,key_hash,fingerprint,state,version,data,attempts,created_at,updated_at,next_at,deadline,lease_token,lease_until) VALUES (@id,@kind,@scope,@key,@fingerprint,@state,@version,@data,@attempts,@created,@updated,@next,@deadline,@lease,@until)", Parameters(record));
            try { return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1; }
            catch (DbException error) when (owner.IsDuplicate(error)) { return false; }
        }
        public async ValueTask<bool> CompareExchangeAsync(OperationRecord record, long expectedVersion, string? expectedLeaseToken = null, CancellationToken token = default)
        {
            owner.Validate(record);
            if (record.Version != checked(expectedVersion + 1)) throw new ResilienceConfigurationException("CAS 必须逐次递增 Version。");
            var parameters = Parameters(record).ToList(); parameters.Add(("@expected", expectedVersion));
            var lease = "";
            if (expectedLeaseToken is not null)
            {
                lease = " AND lease_token=@expectedLease AND lease_until>@now";
                parameters.Add(("@expectedLease", expectedLeaseToken)); parameters.Add(("@now", await Now(token).ConfigureAwait(false)));
            }
            await using var command = Command($"UPDATE {Table} SET state=@state,version=@version,data=@data,attempts=@attempts,updated_at=@updated,next_at=@next,deadline=@deadline,lease_token=@lease,lease_until=@until WHERE id=@id AND kind=@kind AND scope_hash=@scope AND key_hash=@key AND fingerprint=@fingerprint AND version=@expected{lease}", parameters.ToArray());
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }
        public async ValueTask<OperationRecord?> ClaimDueAsync(string kind, IReadOnlyCollection<string> states, TimeSpan leaseDuration, CancellationToken token = default)
        {
            if (states.Count == 0 || states.Count > 32 || leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
                throw new ResilienceConfigurationException("认领状态或租约期限无效。");
            var now = await Now(token).ConfigureAwait(false);
            var parameters = states.Select((s, i) => ("@s" + i, (object?)s)).ToList();
            parameters.Add(("@kind", kind)); parameters.Add(("@now", now));
            var locks = owner._dialect == ResilienceRelationalDialect.MySql ? " FOR UPDATE SKIP LOCKED" : "";
            var index = owner._dialect == ResilienceRelationalDialect.MySql ? $" FORCE INDEX ({owner._prefix}_due)" : "";
            OperationRecord? record;
            await using (var command = Command($"SELECT * FROM {Table}{index} WHERE kind=@kind AND state IN ({string.Join(",", Enumerable.Range(0, states.Count).Select(i => "@s" + i))}) AND (next_at<=@now OR deadline<=@now) AND (lease_until IS NULL OR lease_until<=@now) ORDER BY next_at,id LIMIT 1{locks}", parameters.ToArray()))
            await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                record = await reader.ReadAsync(token).ConfigureAwait(false) ? Read(reader) : null;
            if (record is null) return null;
            var claimed = record with { Version = checked(record.Version + 1), LeaseToken = Guid.NewGuid().ToString("N"), LeaseUntil = now + leaseDuration, UpdatedAt = now };
            return await CompareExchangeAsync(claimed, record.Version, token: token).ConfigureAwait(false) ? claimed : null;
        }
        public async ValueTask<bool> RenewLeaseAsync(string id, long version, string leaseToken, TimeSpan duration, CancellationToken token = default)
        {
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(1)) throw new ResilienceConfigurationException("续租期限无效。");
            var now = await Now(token).ConfigureAwait(false);
            await using var command = Command($"UPDATE {Table} SET lease_until=@until WHERE id=@id AND version=@version AND lease_token=@lease AND lease_until>@now",
                ("@until", now + duration), ("@id", id), ("@version", version), ("@lease", leaseToken), ("@now", now));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) == 1;
        }
        public async ValueTask AppendAuditAsync(OperationAudit audit, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(audit.Actor) || string.IsNullOrWhiteSpace(audit.Reason) || audit.Actor.Length > 128 ||
                string.IsNullOrWhiteSpace(audit.Event) || audit.Event.Length > 64 || Encoding.UTF8.GetByteCount(audit.Reason) > owner._maximumDataBytes)
                throw new ResilienceConfigurationException("审计记录必须包含有界的操作者、事件和原因。");
            await using var command = Command($"INSERT INTO {owner._prefix}_audit (operation_id,version,event_type,actor,reason,at_utc) VALUES (@id,@version,@event,@actor,@reason,@at)",
                ("@id", audit.OperationId), ("@version", audit.Version), ("@event", audit.Event), ("@actor", audit.Actor), ("@reason", audit.Reason), ("@at", audit.At));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        public async ValueTask AppendAttemptAsync(OperationAttempt attempt, CancellationToken token = default)
        {
            if (attempt.Number < 1 || string.IsNullOrWhiteSpace(attempt.LeaseToken) || attempt.LeaseToken.Length > 64 ||
                string.IsNullOrWhiteSpace(attempt.Kind) || attempt.Kind.Length > 64 || attempt.Endpoint?.Length > 256)
                throw new ResilienceConfigurationException("尝试账本参数无效。");
            await using var command = Command($"INSERT INTO {owner._prefix}_attempts (operation_id,attempt_no,kind,lease_token,started_at,completed_at,outcome,generation,endpoint) VALUES (@id,@number,@kind,@lease,@started,@completed,@outcome,@generation,@endpoint)",
                ("@id", attempt.OperationId), ("@number", attempt.Number), ("@kind", attempt.Kind), ("@lease", attempt.LeaseToken),
                ("@started", attempt.StartedAt), ("@completed", attempt.CompletedAt), ("@outcome", attempt.Outcome), ("@generation", attempt.Generation), ("@endpoint", attempt.Endpoint));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        public async ValueTask CompleteAttemptAsync(string operationId, int attempt, string leaseToken, string outcome, DateTimeOffset at, CancellationToken token = default)
        {
            var now = await Now(token).ConfigureAwait(false);
            await using var command = Command($"UPDATE {owner._prefix}_attempts SET completed_at=@at,outcome=@outcome WHERE operation_id=@id AND attempt_no=@number AND lease_token=@lease AND completed_at IS NULL AND EXISTS (SELECT 1 FROM {Table} WHERE id=@id AND lease_token=@lease AND lease_until>@now)",
                ("@at", at), ("@outcome", outcome), ("@id", operationId), ("@number", attempt), ("@lease", leaseToken), ("@now", now));
            if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new OperationConcurrencyException(operationId);
        }
        public async ValueTask RecordAttemptRouteAsync(string operationId, int attempt, string leaseToken, long? generation, string? endpoint, CancellationToken token = default)
        {
            if (endpoint?.Length > 256) throw new ResilienceConfigurationException("尝试端点标识过长。");
            await using var command = Command($"UPDATE {owner._prefix}_attempts SET generation=@generation,endpoint=@endpoint WHERE operation_id=@id AND attempt_no=@attempt AND lease_token=@lease AND completed_at IS NULL AND EXISTS(SELECT 1 FROM {Table} WHERE id=@id AND lease_token=@lease AND lease_until>@now)",
                ("@generation", generation), ("@endpoint", endpoint), ("@id", operationId), ("@attempt", attempt), ("@lease", leaseToken), ("@now", await Now(token).ConfigureAwait(false)));
            if (await command.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new OperationConcurrencyException(operationId);
        }
        public async ValueTask<IReadOnlyList<OperationAttempt>> GetAttemptsAsync(string operationId, CancellationToken token = default)
        {
            var result = new List<OperationAttempt>();
            await using var command = Command($"SELECT * FROM {owner._prefix}_attempts WHERE operation_id=@id ORDER BY attempt_no", ("@id", operationId));
            await using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                result.Add(new(operationId, Convert.ToInt32(reader["attempt_no"], CultureInfo.InvariantCulture), (string)reader["kind"], (string)reader["lease_token"], Date(reader["started_at"]),
                    reader["completed_at"] is DBNull ? null : Date(reader["completed_at"]), reader["outcome"] is DBNull ? null : (string)reader["outcome"],
                    reader["generation"] is DBNull ? null : Convert.ToInt64(reader["generation"], CultureInfo.InvariantCulture), reader["endpoint"] is DBNull ? null : (string)reader["endpoint"]));
            return result.AsReadOnly();
        }
        public async ValueTask CommitAsync(CancellationToken token = default)
        {
            if (!owns) throw new ResilienceConfigurationException("外部事务必须由业务所有者提交。");
            if (_disposed || _finished) throw new ObjectDisposedException(nameof(Session));
            token.ThrowIfCancellationRequested();
            try { await transaction.CommitAsync(token).ConfigureAwait(false); }
            catch (Exception error) { throw new OperationCommitUnknownException(error); }
            finally { _finished = true; }
        }
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return; _disposed = true;
            if (!owns) return;
            try { await transaction.DisposeAsync().ConfigureAwait(false); }
            finally { await connection.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
