using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text;
using Bing.Auditing.Persistence;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证审计存储的 MySQL 清理分支和租户参数编码。
/// </summary>
public sealed class DbAuditingStoreMySqlCommandTest
{
    /// <summary>
    /// MySQL 租户清理应使用稳定删除语句并以严格 UTF-8 二进制参数传递租户。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenMySql_ShouldUseTenantSqlAndBinaryParameter()
    {
        RecordingDbConnection connection = null;
        var store = new DbAuditingStore(AuditLogDatabase.MySql,
            () => connection = new RecordingDbConnection());
        var tenant = " café ";
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var deleted = await ((IAuditLogTenantRetentionStore)store)
            .DeleteBeforeAsync(tenant, cutoff, 7);

        Assert.Equal(1, deleted);
        Assert.NotNull(connection);
        Assert.Equal(
            "DELETE FROM `bing_audit_log` WHERE TenantId = @tenant AND IsCritical = 0 " +
            "AND ExecutionTimeUtc < @cutoff ORDER BY ExecutionTimeUtc, AuditId LIMIT 7",
            connection.Command.CommandText);
        var tenantParameter = connection.Command.Parameters["@tenant"];
        Assert.Equal(Encoding.UTF8.GetBytes(tenant), Assert.IsType<byte[]>(tenantParameter.Value));
        Assert.Equal(cutoff, connection.Command.Parameters["@cutoff"].Value);
    }

    /// <summary>
    /// MySQL 归档应先复制再删除，并使用归档时间参数。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenMySql_ShouldUseInsertThenDeleteSql()
    {
        RecordingDbConnection connection = null;
        var store = new DbAuditingStore(AuditLogDatabase.MySql,
            () => connection = new RecordingDbConnection());
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var archived = await ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 7);

        Assert.Equal(1, archived);
        Assert.Equal(2, connection.Commands.Count);
        Assert.Equal(
            "INSERT INTO `bing_audit_log_archive` (TenantId, AuditId, ApplicationName, UserId, " +
            "ExecutionTimeUtc, ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, " +
            "IsCritical, ArchivedAtUtc) SELECT TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, " +
            "ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, IsCritical, @archived " +
            "FROM `bing_audit_log` WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
            "ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT 7",
            connection.Commands[0].CommandText);
        Assert.Equal(
            "DELETE source FROM `bing_audit_log` AS source INNER JOIN `bing_audit_log_archive` AS archive " +
            "ON archive.TenantId = source.TenantId AND archive.AuditId = source.AuditId " +
            "AND archive.ArchivedAtUtc = @archived WHERE source.IsCritical = 0 " +
            "AND source.ExecutionTimeUtc < @cutoff",
            connection.Commands[1].CommandText);
        Assert.Equal(cutoff, connection.Commands[0].Parameters["@cutoff"].Value);
        Assert.IsType<DateTime>(connection.Commands[0].Parameters["@archived"].Value);
        Assert.Equal(connection.Commands[0].Parameters["@archived"].Value,
            connection.Commands[1].Parameters["@archived"].Value);
    }

    /// <summary>
    /// 归档和删除影响行数不一致时应拒绝提交。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenBatchCountsDiffer_ShouldRollback()
    {
        RecordingDbConnection connection = null;
        var store = new DbAuditingStore(AuditLogDatabase.MySql, () =>
        {
            connection = new RecordingDbConnection(2, 1);
            return connection;
        });
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 7));

        Assert.Contains("归档批次不一致", exception.Message, StringComparison.Ordinal);
        Assert.True(connection.RollbackCalled);
    }

    /// <summary>
    /// MySQL 归档历史清理应按归档时间使用有界删除语句。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenMySql_ShouldUseArchiveRetentionSql()
    {
        RecordingDbConnection connection = null;
        var store = new DbAuditingStore(AuditLogDatabase.MySql,
            () => connection = new RecordingDbConnection());
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var deleted = await ((IAuditLogArchiveRetentionStore)store)
            .DeleteBeforeAsync(cutoff, 7);

        Assert.Equal(1, deleted);
        Assert.Equal(
            "DELETE FROM `bing_audit_log_archive` WHERE IsCritical = 0 AND ArchivedAtUtc < @cutoff " +
            "ORDER BY ArchivedAtUtc, TenantId, AuditId LIMIT 7",
            connection.Command.CommandText);
        Assert.Equal(cutoff, connection.Command.Parameters["@cutoff"].Value);
    }

    /// <summary>
    /// 记录测试命令及其事务边界。
    /// </summary>
    private sealed class RecordingDbConnection : DbConnection
    {
        /// <summary>
        /// 当前连接状态。
        /// </summary>
        private ConnectionState _state = ConnectionState.Closed;

        /// <summary>
        /// 初始化一个 <see cref="RecordingDbConnection" /> 类型的实例。
        /// </summary>
        /// <param name="nonQueryResults">按命令顺序返回的影响行数。</param>
        public RecordingDbConnection(params int[] nonQueryResults)
        {
            NonQueryResults = new Queue<int>(nonQueryResults ?? Array.Empty<int>());
        }

        /// <summary>
        /// 获取测试命令的影响行数队列。
        /// </summary>
        public Queue<int> NonQueryResults { get; }

        /// <summary>
        /// 获取事务是否执行过回滚。
        /// </summary>
        public bool RollbackCalled { get; private set; }

        /// <summary>
        /// 标记测试事务已回滚。
        /// </summary>
        public void MarkRollback() => RollbackCalled = true;

        /// <summary>
        /// 获取最后创建的测试命令。
        /// </summary>
        public RecordingDbCommand Command { get; private set; }

        /// <summary>
        /// 获取创建过的测试命令。
        /// </summary>
        public List<RecordingDbCommand> Commands { get; } = new();

        /// <inheritdoc />
        public override string ConnectionString { get; set; }

        /// <inheritdoc />
        public override string Database => "recording";

        /// <inheritdoc />
        public override string DataSource => "recording";

        /// <inheritdoc />
        public override string ServerVersion => "1.0";

        /// <inheritdoc />
        public override ConnectionState State => _state;

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName)
        {
        }

        /// <inheritdoc />
        public override void Close() => _state = ConnectionState.Closed;

        /// <inheritdoc />
        public override void Open() => _state = ConnectionState.Open;

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new RecordingDbTransaction(this);

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand() =>
            Command = CreateRecordedCommand();

        /// <summary>
        /// 创建并记录测试命令。
        /// </summary>
        private RecordingDbCommand CreateRecordedCommand()
        {
            var command = new RecordingDbCommand(this);
            Commands.Add(command);
            return command;
        }
    }

    /// <summary>
    /// 记录事务提交和回滚调用的测试事务。
    /// </summary>
    private sealed class RecordingDbTransaction : DbTransaction
    {
        /// <summary>
        /// 初始化一个 <see cref="RecordingDbTransaction" /> 类型的实例。
        /// </summary>
        /// <param name="connection">所属连接。</param>
        public RecordingDbTransaction(DbConnection connection) => DbConnectionValue = connection;

        /// <summary>
        /// 获取所属连接。
        /// </summary>
        private DbConnection DbConnectionValue { get; }

        /// <inheritdoc />
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <inheritdoc />
        protected override DbConnection DbConnection => DbConnectionValue;

        /// <inheritdoc />
        public override void Commit()
        {
        }

        /// <inheritdoc />
        public override void Rollback() => ((RecordingDbConnection)DbConnectionValue).MarkRollback();
    }

    /// <summary>
    /// 记录 SQL 和参数的测试命令。
    /// </summary>
    private sealed class RecordingDbCommand : DbCommand
    {
        /// <summary>
        /// 初始化一个 <see cref="RecordingDbCommand" /> 类型的实例。
        /// </summary>
        /// <param name="connection">所属连接。</param>
        public RecordingDbCommand(DbConnection connection)
        {
            DbConnectionValue = connection;
            DbParameterCollectionValue = new RecordingDbParameterCollection();
        }

        /// <summary>
        /// 获取所属连接。
        /// </summary>
        private DbConnection DbConnectionValue { get; }

        /// <summary>
        /// 获取测试参数集合。
        /// </summary>
        /// <inheritdoc />
        public override string CommandText { get; set; }

        /// <inheritdoc />
        public override int CommandTimeout { get; set; }

        /// <inheritdoc />
        public override CommandType CommandType { get; set; }

        /// <inheritdoc />
        public override bool DesignTimeVisible { get; set; }

        /// <inheritdoc />
        public override UpdateRowSource UpdatedRowSource { get; set; }

        /// <inheritdoc />
        protected override DbConnection DbConnection
        {
            get => DbConnectionValue;
            set { }
        }

        /// <inheritdoc />
        protected override DbParameterCollection DbParameterCollection => DbParameterCollectionValue;

        /// <summary>
        /// 测试参数集合。
        /// </summary>
        private RecordingDbParameterCollection DbParameterCollectionValue { get; }

        /// <inheritdoc />
        protected override DbTransaction DbTransaction { get; set; }

        /// <inheritdoc />
        public override void Cancel()
        {
        }

        /// <inheritdoc />
        public override int ExecuteNonQuery() =>
            ((RecordingDbConnection)DbConnectionValue).NonQueryResults.Count == 0
                ? 1
                : ((RecordingDbConnection)DbConnectionValue).NonQueryResults.Dequeue();

        /// <inheritdoc />
        public override object ExecuteScalar() => 1;

        /// <inheritdoc />
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public override void Prepare()
        {
        }

        /// <inheritdoc />
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
    }

    /// <summary>
    /// 保存测试参数的集合实现。
    /// </summary>
    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        /// <summary>
        /// 测试参数列表。
        /// </summary>
        private readonly List<DbParameter> _items = new();

        /// <inheritdoc />
        public override int Count => _items.Count;

        /// <inheritdoc />
        public override object SyncRoot => ((ICollection)_items).SyncRoot;

        /// <inheritdoc />
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        /// <inheritdoc />
        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        /// <inheritdoc />
        public override void Clear() => _items.Clear();

        /// <inheritdoc />
        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        /// <inheritdoc />
        public override bool Contains(string value) => IndexOf(value) >= 0;

        /// <inheritdoc />
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        /// <inheritdoc />
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        /// <inheritdoc />
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        /// <inheritdoc />
        public override int IndexOf(string parameterName) =>
            _items.FindIndex(item => string.Equals(item.ParameterName, parameterName, StringComparison.Ordinal));

        /// <inheritdoc />
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        /// <inheritdoc />
        public override void Remove(object value) => _items.Remove((DbParameter)value);

        /// <inheritdoc />
        public override void RemoveAt(int index) => _items.RemoveAt(index);

        /// <inheritdoc />
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

        /// <inheritdoc />
        protected override DbParameter GetParameter(int index) => _items[index];

        /// <inheritdoc />
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        /// <inheritdoc />
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        /// <inheritdoc />
        protected override void SetParameter(string parameterName, DbParameter value) =>
            _items[IndexOf(parameterName)] = value;
    }

    /// <summary>
    /// 保存测试参数值的参数实现。
    /// </summary>
    private sealed class RecordingDbParameter : DbParameter
    {
        /// <inheritdoc />
        public override DbType DbType { get; set; }

        /// <inheritdoc />
        public override ParameterDirection Direction { get; set; }

        /// <inheritdoc />
        public override bool IsNullable { get; set; }

        /// <inheritdoc />
        public override string ParameterName { get; set; }

        /// <inheritdoc />
        public override int Size { get; set; }

        /// <inheritdoc />
        public override string SourceColumn { get; set; }

        /// <inheritdoc />
        public override bool SourceColumnNullMapping { get; set; }

        /// <inheritdoc />
        public override object Value { get; set; }

        /// <inheritdoc />
        public override void ResetDbType()
        {
        }
    }
}
