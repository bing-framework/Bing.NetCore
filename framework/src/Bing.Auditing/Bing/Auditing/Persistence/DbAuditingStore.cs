using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using Bing.Data.Transaction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bing.Auditing.Persistence;
/// <summary>
/// 将安全投影后的审计日志保存到数据库。
/// </summary>
/// <remarks>普通审计使用独立自有连接，关键审计仅复用传入事务。连接工厂必须每次返回新的未打开连接。netstandard2.0 下事务提交与资源释放使用同步数据库 API。</remarks>
public sealed class DbAuditingStore : ICancellableAuditingStore, ITransactionalAuditingStore,
    IAuditLogRetentionStore, IAuditLogArchiveStore, IAuditLogArchiveRetentionStore,
    IAuditLogTenantRetentionStore, IAuditLogQueryStore
{
    /// <summary>
    /// 单次普通审计清理允许的最大批量。
    /// </summary>
    private const int MaxRetentionBatchSize = 10000;
    /// <summary>
    /// 租户标识使用的严格 UTF-8 编码器。
    /// </summary>
    private static readonly Encoding TenantEncoding = new UTF8Encoding(false, true);
    /// <summary>
    /// 决定租户参数使用二进制还是文本的数据库语法。
    /// </summary>
    private readonly AuditLogDatabase _database;
    /// <summary>
    /// 独立保存所用的连接工厂。
    /// </summary>
    private readonly Func<DbConnection> _connectionFactory;
    /// <summary>
    /// 固定的插入语句。
    /// </summary>
    private readonly string _insertSql;
    /// <summary>
    /// 分批清理普通审计日志的语句模板。
    /// </summary>
    private readonly string _retentionSqlTemplate;
    /// <summary>
    /// 将普通审计日志复制到归档表的语句模板。
    /// </summary>
    private readonly string _archiveInsertSqlTemplate;
    /// <summary>
    /// 归档成功后从主表删除相同批次记录的语句模板。
    /// </summary>
    private readonly string _archiveDeleteSqlTemplate;
    /// <summary>
    /// 清理归档表历史记录的语句模板。
    /// </summary>
    private readonly string _archiveRetentionSqlTemplate;
    /// <summary>
    /// 分批清理指定租户普通审计日志的语句模板。
    /// </summary>
    private readonly string _tenantRetentionSqlTemplate;
    /// <summary>
    /// 审计元数据查询的语句模板。
    /// </summary>
    private readonly string _querySqlTemplate;
    /// <summary>
    /// 属性值保存白名单。
    /// </summary>
    private readonly HashSet<string> _allowedProperties;
    /// <summary>
    /// 文本长度上限。
    /// </summary>
    private readonly int _maxFieldLength;
    /// <summary>
    /// 明细字节上限。
    /// </summary>
    private readonly int _maxPayloadBytes;
    /// <summary>
    /// 明细数量上限。
    /// </summary>
    private readonly int _maxDetailCount;
    /// <summary>
    /// 初始化一个 <see cref="DbAuditingStore" /> 类型的实例。
    /// </summary>
    /// <param name="database">数据库语法。</param>
    /// <param name="connectionFactory">普通审计的自有连接工厂；仅使用关键审计时可省略。</param>
    /// <param name="options">持久化规则；构造时建立快照。</param>
    public DbAuditingStore(AuditLogDatabase database, Func<DbConnection> connectionFactory = null,
        AuditLogPersistenceOptions options = null)
    {
        var table = database == AuditLogDatabase.MySql ? "`bing_audit_log`" :
            database == AuditLogDatabase.Sqlite ? "bing_audit_log" :
            throw new ArgumentOutOfRangeException(nameof(database));
        options = options ?? new AuditLogPersistenceOptions();
        if (options.MaxFieldLength <= 0 || options.MaxPayloadBytes <= 0 || options.MaxDetailCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "审计大小限制必须大于零。");
        _connectionFactory = connectionFactory;
        _database = database;
        _allowedProperties = new HashSet<string>(options.AllowedPropertyNames, StringComparer.Ordinal);
        _maxFieldLength = options.MaxFieldLength;
        _maxPayloadBytes = options.MaxPayloadBytes;
        _maxDetailCount = options.MaxDetailCount;
        _insertSql = $"INSERT INTO {table} (AuditId, TenantId, ApplicationName, UserId, ExecutionTimeUtc, " +
                     "ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, IsCritical) " +
                     "VALUES (@id, @tenant, @app, @user, @time, @duration, @method, @status, @correlation, @details, @critical)";
        _retentionSqlTemplate = database == AuditLogDatabase.MySql
            ? "DELETE FROM `bing_audit_log` WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {0}"
            : "DELETE FROM bing_audit_log WHERE rowid IN (SELECT rowid FROM bing_audit_log " +
              "WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {0})";
        _archiveInsertSqlTemplate = database == AuditLogDatabase.MySql
            ? "INSERT INTO `bing_audit_log_archive` (TenantId, AuditId, ApplicationName, UserId, " +
              "ExecutionTimeUtc, ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, " +
              "IsCritical, ArchivedAtUtc) SELECT TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, " +
              "ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, IsCritical, @archived " +
              "FROM `bing_audit_log` WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {0}"
            : "INSERT INTO bing_audit_log_archive (TenantId, AuditId, ApplicationName, UserId, " +
              "ExecutionTimeUtc, ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, " +
              "IsCritical, ArchivedAtUtc) SELECT TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, " +
              "ExecutionDuration, HttpMethod, HttpStatusCode, CorrelationId, DetailsJson, IsCritical, @archived " +
              "FROM bing_audit_log WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {0}";
        _archiveDeleteSqlTemplate = database == AuditLogDatabase.MySql
            ? "DELETE source FROM `bing_audit_log` AS source INNER JOIN `bing_audit_log_archive` AS archive " +
              "ON archive.TenantId = source.TenantId AND archive.AuditId = source.AuditId " +
              "AND archive.ArchivedAtUtc = @archived WHERE source.IsCritical = 0 " +
              "AND source.ExecutionTimeUtc < @cutoff"
            : "DELETE FROM bing_audit_log WHERE IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "AND EXISTS (SELECT 1 FROM bing_audit_log_archive AS archive " +
              "WHERE archive.TenantId = bing_audit_log.TenantId " +
              "AND archive.AuditId = bing_audit_log.AuditId " +
              "AND archive.ArchivedAtUtc = @archived)";
        _archiveRetentionSqlTemplate = database == AuditLogDatabase.MySql
            ? "DELETE FROM `bing_audit_log_archive` WHERE IsCritical = 0 AND ArchivedAtUtc < @cutoff " +
              "ORDER BY ArchivedAtUtc, TenantId, AuditId LIMIT {0}"
            : "DELETE FROM bing_audit_log_archive WHERE rowid IN (SELECT rowid FROM bing_audit_log_archive " +
              "WHERE IsCritical = 0 AND ArchivedAtUtc < @cutoff " +
              "ORDER BY ArchivedAtUtc, TenantId, AuditId LIMIT {0})";
        _tenantRetentionSqlTemplate = database == AuditLogDatabase.MySql
            ? "DELETE FROM `bing_audit_log` WHERE TenantId = @tenant AND IsCritical = 0 " +
              "AND ExecutionTimeUtc < @cutoff ORDER BY ExecutionTimeUtc, AuditId LIMIT {0}"
            : "DELETE FROM bing_audit_log WHERE rowid IN (SELECT rowid FROM bing_audit_log " +
              "WHERE TenantId = @tenant AND IsCritical = 0 AND ExecutionTimeUtc < @cutoff " +
              "ORDER BY ExecutionTimeUtc, AuditId LIMIT {0})";
        _querySqlTemplate = database == AuditLogDatabase.MySql
            ? "SELECT TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, ExecutionDuration, " +
              "HttpMethod, HttpStatusCode, CorrelationId, IsCritical FROM `bing_audit_log` " +
              "WHERE {0} ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {1} OFFSET {2}"
            : "SELECT TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, ExecutionDuration, " +
              "HttpMethod, HttpStatusCode, CorrelationId, IsCritical FROM bing_audit_log " +
              "WHERE {0} ORDER BY ExecutionTimeUtc, TenantId, AuditId LIMIT {1} OFFSET {2}";
    }
    /// <inheritdoc />
    public Task SaveAsync(AuditLogInfo auditInfo) => SaveAsync(auditInfo, CancellationToken.None);
    /// <inheritdoc />
    /// <remarks>
    /// <para>创建并释放自己的连接与事务；不会复用业务事务。提交确认失败记录 Unknown，不自动重试。</para>
    /// <para>保存普通审计日志。</para>
    /// </remarks>
    public async Task SaveAsync(AuditLogInfo auditInfo, CancellationToken cancellationToken)
    {
        if (auditInfo == null)
            throw new ArgumentNullException(nameof(auditInfo));
        if (_connectionFactory == null)
            throw new InvalidOperationException("普通审计保存必须配置自有连接工厂。");
        cancellationToken.ThrowIfCancellationRequested();
        DbConnection connection = null;
        DbTransaction transaction = null;
        Exception failure = null;
        var cleanup = new List<Exception>();
        var outcome = TransactionOutcome.Unknown;
        var commitAttempted = false;
        try
        {
            var created = _connectionFactory() ?? throw new InvalidOperationException("审计连接工厂返回了 null。");
            if (created.State != ConnectionState.Closed)
                throw new InvalidOperationException("审计连接工厂必须返回新的未打开连接。");
            connection = created;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = connection.BeginTransaction();
            await SaveAsync(transaction, Guid.NewGuid().ToString("N"), auditInfo, cancellationToken, false)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            transaction.Commit();
            outcome = TransactionOutcome.Committed;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (transaction != null)
            {
                try
                {
                    transaction.Rollback();
                    if (!commitAttempted)
                        outcome = TransactionOutcome.RolledBack;
                }
                catch (Exception rollbackError) { cleanup.Add(rollbackError); }
            }
        }
        finally
        {
            DisposeResource(transaction, cleanup);
            DisposeResource(connection, cleanup);
        }
        if (failure != null)
        {
            failure.SetTransactionOutcome(outcome);
            foreach (var exception in cleanup)
                AddCleanupError(failure, exception);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (cleanup.Count != 0)
        {
            var exception = new AggregateException("审计保存后的资源清理失败。", cleanup);
            exception.SetTransactionOutcome(outcome);
            throw exception;
        }
    }
    /// <inheritdoc />
    public Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
        CancellationToken cancellationToken = default) =>
        SaveAsync(transaction, auditId, auditInfo, cancellationToken, true);

    /// <summary>
    /// 在当前事务内保存审计日志并标记其类型。
    /// </summary>
    /// <param name="transaction">调用方当前事务。</param>
    /// <param name="auditId">当前租户内稳定且唯一的审计标识。</param>
    /// <param name="auditInfo">审计信息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="critical">是否标记为关键审计。</param>
    private async Task SaveAsync(DbTransaction transaction, string auditId, AuditLogInfo auditInfo,
        CancellationToken cancellationToken, bool critical)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (auditInfo == null)
            throw new ArgumentNullException(nameof(auditInfo));
        var connection = transaction.Connection ?? throw new InvalidOperationException("审计事务没有活动连接。");
        cancellationToken.ThrowIfCancellationRequested();
        var id = Identity(auditId, nameof(auditId), false);
        var tenant = TenantIdentity(auditInfo.TenantId);
        var user = Identity(auditInfo.UserId, nameof(auditInfo.UserId), true);
        if (auditInfo.ExecutionDuration < 0)
            throw new ArgumentOutOfRangeException(nameof(auditInfo), "执行时长不能为负数。");
        var details = CreateDetails(auditInfo);
        var time = auditInfo.ExecutionTime == default ? DateTime.UtcNow : auditInfo.ExecutionTime;
        if (time.Kind == DateTimeKind.Local)
            time = time.ToUniversalTime();
        DbCommand command = null;
        Exception failure = null;
        try
        {
            command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = _insertSql;
            AddParameter(command, "@id", id);
            AddParameter(command, "@tenant", _database == AuditLogDatabase.MySql
                ? (object)TenantEncoding.GetBytes(tenant) : tenant);
            AddParameter(command, "@app", Text(auditInfo.ApplicationName, 256));
            AddParameter(command, "@user", user);
            AddParameter(command, "@time", time);
            AddParameter(command, "@duration", auditInfo.ExecutionDuration);
            AddParameter(command, "@method", Text(auditInfo.HttpMethod, 32));
            AddParameter(command, "@status", auditInfo.HttpStatusCode);
            AddParameter(command, "@correlation", Text(auditInfo.CorrelationId, 128));
            AddParameter(command, "@details", details);
            AddParameter(command, "@critical", critical ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            try { command?.Dispose(); }
            catch (Exception cleanupError) when (failure != null) { AddCleanupError(failure, cleanupError); }
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default) =>
        DeleteBeforeCoreAsync(null, cutoffUtc, batchSize, cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteBeforeAsync(string tenantId, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default)
    {
        var tenant = AuditLogTenantIdentity.Validate(tenantId, nameof(tenantId), true);
        return DeleteBeforeCoreAsync(tenant, cutoffUtc, batchSize, cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> ArchiveBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken = default) =>
        ArchiveBeforeCoreAsync(cutoffUtc, batchSize, cancellationToken);

    /// <inheritdoc />
    Task<int> IAuditLogArchiveRetentionStore.DeleteBeforeAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken) =>
        DeleteArchivedBeforeCoreAsync(cutoffUtc, batchSize, cancellationToken);

    /// <summary>
    /// 批量归档普通审计日志。
    /// </summary>
    /// <param name="cutoffUtc">归档截止时间。</param>
    /// <param name="batchSize">本次最多归档的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本批次已归档并从普通日志表删除的记录数。</returns>
    /// <remarks>在同一自有事务内复制到归档表并删除原记录；复制数与删除数必须一致。</remarks>
    private async Task<int> ArchiveBeforeCoreAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计归档截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxRetentionBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"审计归档批量必须位于 1 到 {MaxRetentionBatchSize} 之间。");
        if (_connectionFactory == null)
            throw new InvalidOperationException("审计归档必须配置自有连接工厂。");

        cancellationToken.ThrowIfCancellationRequested();
        DbConnection connection = null;
        DbTransaction transaction = null;
        DbCommand archiveCommand = null;
        DbCommand deleteCommand = null;
        Exception failure = null;
        var cleanup = new List<Exception>();
        var outcome = TransactionOutcome.Unknown;
        var commitAttempted = false;
        var archived = 0;
        var inserted = 0;
        var archivedAtUtc = DateTime.UtcNow;
        try
        {
            var created = _connectionFactory() ?? throw new InvalidOperationException("审计连接工厂返回了 null。");
            if (created.State != ConnectionState.Closed)
                throw new InvalidOperationException("审计连接工厂必须返回新的未打开连接。");
            connection = created;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = connection.BeginTransaction();

            archiveCommand = connection.CreateCommand();
            archiveCommand.Transaction = transaction;
            archiveCommand.CommandText = string.Format(CultureInfo.InvariantCulture,
                _archiveInsertSqlTemplate, batchSize);
            AddParameter(archiveCommand, "@cutoff", cutoffUtc);
            AddParameter(archiveCommand, "@archived", archivedAtUtc);
            inserted = await archiveCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = string.Format(CultureInfo.InvariantCulture,
                _archiveDeleteSqlTemplate, batchSize);
            AddParameter(deleteCommand, "@cutoff", cutoffUtc);
            AddParameter(deleteCommand, "@archived", archivedAtUtc);
            archived = await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (archived != inserted)
                throw new InvalidOperationException(
                    $"审计归档批次不一致：写入 {inserted} 条，删除 {archived} 条。事务已回滚。");
            commitAttempted = true;
            transaction.Commit();
            outcome = TransactionOutcome.Committed;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (transaction != null)
            {
                try
                {
                    transaction.Rollback();
                    if (!commitAttempted)
                        outcome = TransactionOutcome.RolledBack;
                }
                catch (Exception rollbackError) { cleanup.Add(rollbackError); }
            }
        }
        finally
        {
            DisposeResource(deleteCommand, cleanup);
            DisposeResource(archiveCommand, cleanup);
            DisposeResource(transaction, cleanup);
            DisposeResource(connection, cleanup);
        }
        if (failure != null)
        {
            failure.SetTransactionOutcome(outcome);
            foreach (var exception in cleanup)
                AddCleanupError(failure, exception);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (cleanup.Count != 0)
        {
            var exception = new AggregateException("审计归档后的资源清理失败。", cleanup);
            exception.SetTransactionOutcome(outcome);
            throw exception;
        }
        return archived;
    }

    /// <summary>
    /// 批量删除审计归档历史记录。
    /// </summary>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本批次删除的归档历史记录数。</returns>
    /// <remarks>清理在自有连接与事务内完成。</remarks>
    private async Task<int> DeleteArchivedBeforeCoreAsync(DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计归档历史清理截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxRetentionBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"审计归档历史清理批量必须位于 1 到 {MaxRetentionBatchSize} 之间。");
        if (_connectionFactory == null)
            throw new InvalidOperationException("审计归档历史清理必须配置自有连接工厂。");

        cancellationToken.ThrowIfCancellationRequested();
        DbConnection connection = null;
        DbTransaction transaction = null;
        DbCommand command = null;
        Exception failure = null;
        var cleanup = new List<Exception>();
        var outcome = TransactionOutcome.Unknown;
        var commitAttempted = false;
        var deleted = 0;
        try
        {
            var created = _connectionFactory() ?? throw new InvalidOperationException("审计连接工厂返回了 null。");
            if (created.State != ConnectionState.Closed)
                throw new InvalidOperationException("审计连接工厂必须返回新的未打开连接。");
            connection = created;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = connection.BeginTransaction();
            command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = string.Format(CultureInfo.InvariantCulture,
                _archiveRetentionSqlTemplate, batchSize);
            AddParameter(command, "@cutoff", cutoffUtc);
            deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            transaction.Commit();
            outcome = TransactionOutcome.Committed;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (transaction != null)
            {
                try
                {
                    transaction.Rollback();
                    if (!commitAttempted)
                        outcome = TransactionOutcome.RolledBack;
                }
                catch (Exception rollbackError) { cleanup.Add(rollbackError); }
            }
        }
        finally
        {
            DisposeResource(command, cleanup);
            DisposeResource(transaction, cleanup);
            DisposeResource(connection, cleanup);
        }
        if (failure != null)
        {
            failure.SetTransactionOutcome(outcome);
            foreach (var exception in cleanup)
                AddCleanupError(failure, exception);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (cleanup.Count != 0)
        {
            var exception = new AggregateException("审计归档历史清理后的资源清理失败。", cleanup);
            exception.SetTransactionOutcome(outcome);
            throw exception;
        }
        return deleted;
    }

    /// <summary>
    /// 批量清理全局或指定租户的审计日志。
    /// </summary>
    /// <param name="tenantId">租户标识；为 <see langword="null" /> 时清理所有租户。</param>
    /// <param name="cutoffUtc">保留截止时间。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本批次删除的普通审计日志记录数。</returns>
    /// <remarks>清理在自有连接与事务内完成。</remarks>
    private async Task<int> DeleteBeforeCoreAsync(string tenantId, DateTime cutoffUtc, int batchSize,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("审计保留截止时间必须使用 UTC。", nameof(cutoffUtc));
        if (batchSize <= 0 || batchSize > MaxRetentionBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize),
                $"审计清理批量必须位于 1 到 {MaxRetentionBatchSize} 之间。");
        if (_connectionFactory == null)
            throw new InvalidOperationException("审计清理必须配置自有连接工厂。");

        cancellationToken.ThrowIfCancellationRequested();
        DbConnection connection = null;
        DbTransaction transaction = null;
        DbCommand command = null;
        Exception failure = null;
        var cleanup = new List<Exception>();
        var outcome = TransactionOutcome.Unknown;
        var commitAttempted = false;
        var deleted = 0;
        try
        {
            var created = _connectionFactory() ?? throw new InvalidOperationException("审计连接工厂返回了 null。");
            if (created.State != ConnectionState.Closed)
                throw new InvalidOperationException("审计连接工厂必须返回新的未打开连接。");
            connection = created;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            transaction = connection.BeginTransaction();
            command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                tenantId == null ? _retentionSqlTemplate : _tenantRetentionSqlTemplate, batchSize);
            if (tenantId != null)
                AddParameter(command, "@tenant", _database == AuditLogDatabase.MySql
                    ? (object)TenantEncoding.GetBytes(tenantId) : tenantId);
            AddParameter(command, "@cutoff", cutoffUtc);
            deleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            commitAttempted = true;
            transaction.Commit();
            outcome = TransactionOutcome.Committed;
        }
        catch (Exception exception)
        {
            failure = exception;
            if (transaction != null)
            {
                try
                {
                    transaction.Rollback();
                    if (!commitAttempted)
                        outcome = TransactionOutcome.RolledBack;
                }
                catch (Exception rollbackError) { cleanup.Add(rollbackError); }
            }
        }
        finally
        {
            DisposeResource(command, cleanup);
            DisposeResource(transaction, cleanup);
            DisposeResource(connection, cleanup);
        }
        if (failure != null)
        {
            failure.SetTransactionOutcome(outcome);
            foreach (var exception in cleanup)
                AddCleanupError(failure, exception);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (cleanup.Count != 0)
        {
            var exception = new AggregateException("审计清理后的资源清理失败。", cleanup);
            exception.SetTransactionOutcome(outcome);
            throw exception;
        }
        return deleted;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        AuditLogQueryValidator.Validate(query);
        var tenant = TenantIdentity(query.TenantId);
        if (_connectionFactory == null)
            throw new InvalidOperationException("审计查询必须配置自有连接工厂。");

        cancellationToken.ThrowIfCancellationRequested();
        DbConnection connection = null;
        DbCommand command = null;
        Exception failure = null;
        var cleanup = new List<Exception>();
        List<AuditLogEntry> entries = null;
        try
        {
            var created = _connectionFactory() ?? throw new InvalidOperationException("审计连接工厂返回了 null。");
            if (created.State != ConnectionState.Closed)
                throw new InvalidOperationException("审计连接工厂必须返回新的未打开连接。");
            connection = created;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            command = connection.CreateCommand();
            var predicates = new List<string> { "TenantId = @tenant" };
            AddParameter(command, "@tenant", _database == AuditLogDatabase.MySql
                ? (object)TenantEncoding.GetBytes(tenant) : tenant);
            AddQueryParameter(command, predicates, "ApplicationName", query.ApplicationName);
            AddQueryParameter(command, predicates, "UserId", query.UserId);
            AddQueryParameter(command, predicates, "CorrelationId", query.CorrelationId);
            if (query.FromUtc.HasValue)
            {
                predicates.Add("ExecutionTimeUtc >= @from");
                AddParameter(command, "@from", query.FromUtc.Value);
            }
            if (query.ToUtc.HasValue)
            {
                predicates.Add("ExecutionTimeUtc < @to");
                AddParameter(command, "@to", query.ToUtc.Value);
            }
            if (query.IsCritical.HasValue)
            {
                predicates.Add("IsCritical = @critical");
                AddParameter(command, "@critical", query.IsCritical.Value ? 1 : 0);
            }
            command.CommandText = string.Format(CultureInfo.InvariantCulture, _querySqlTemplate,
                string.Join(" AND ", predicates), query.Limit, query.Offset);
            entries = new List<AuditLogEntry>();
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entries.Add(new AuditLogEntry(
                        ReadString(reader, 0), ReadString(reader, 1), ReadNullableString(reader, 2),
                        ReadNullableString(reader, 3), ReadUtc(reader, 4), reader.GetInt32(5),
                        ReadNullableString(reader, 6), ReadNullableInt(reader, 7),
                        ReadNullableString(reader, 8), reader.GetInt32(9) != 0));
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            DisposeResource(command, cleanup);
            DisposeResource(connection, cleanup);
        }
        if (failure != null)
        {
            foreach (var exception in cleanup)
                AddCleanupError(failure, exception);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        if (cleanup.Count != 0)
            throw new AggregateException("审计查询后的资源清理失败。", cleanup);
        return entries;
    }

    /// <summary>
    /// 添加可选文本查询条件。
    /// </summary>
    /// <param name="command">接收参数的数据库命令。</param>
    /// <param name="predicates">用于收集查询条件的集合。</param>
    /// <param name="column">需要筛选的数据库列名。</param>
    /// <param name="value">筛选条件的文本值；为 null 时不增加条件。</param>
    private static void AddQueryParameter(DbCommand command, ICollection<string> predicates,
        string column, string value)
    {
        if (value == null)
            return;
        var parameterName = "@query_" + column;
        predicates.Add(column + " = " + parameterName);
        AddParameter(command, parameterName, value);
    }

    /// <summary>
    /// 读取必需文本列。
    /// </summary>
    /// <param name="reader">已定位到当前记录的数据读取器。</param>
    /// <param name="ordinal">从零开始的列序号。</param>
    /// <returns>列值对应的文本；数据库空值返回空字符串，字节数组按租户编码解码。</returns>
    private static string ReadString(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return string.Empty;
        var value = reader.GetValue(ordinal);
        return value is byte[] bytes
            ? TenantEncoding.GetString(bytes)
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 读取可空文本列。
    /// </summary>
    /// <param name="reader">已定位到当前记录的数据读取器。</param>
    /// <param name="ordinal">从零开始的列序号。</param>
    /// <returns>列值对应的文本；数据库空值返回 null。</returns>
    private static string ReadNullableString(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 读取可空整数列。
    /// </summary>
    /// <param name="reader">已定位到当前记录的数据读取器。</param>
    /// <param name="ordinal">从零开始的列序号。</param>
    /// <returns>列值对应的整数；数据库空值返回 null。</returns>
    private static int? ReadNullableInt(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? (int?)null : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 读取 UTC 时间列。
    /// </summary>
    /// <param name="reader">已定位到当前记录的数据读取器。</param>
    /// <param name="ordinal">从零开始的列序号。</param>
    /// <returns>标记为 UTC 的时间；数据库空值抛出异常。</returns>
    private static DateTime ReadUtc(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            throw new InvalidOperationException("审计执行时间不能为 null。");
        var value = reader.GetValue(ordinal);
        var time = value is DateTime dateTime
            ? dateTime
            : DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
        return DateTime.SpecifyKind(time, DateTimeKind.Utc);
    }

    /// <summary>
    /// 生成审计日志的持久化明细。
    /// </summary>
    /// <param name="info">需要生成持久化明细的审计日志。</param>
    /// <returns>包含操作、实体变更和异常类型的紧凑 JSON 文本。</returns>
    /// <remarks>仅保存受控字段和异常类型，不序列化任意对象；受字段长度、明细数量和 UTF-8 总字节数限制。</remarks>
    private string CreateDetails(AuditLogInfo info)
    {
        var actions = new JArray();
        var changes = new JArray();
        var exceptions = new JArray();
        var count = 0;
        foreach (var action in info.Actions ?? Enumerable.Empty<AuditLogActionInfo>())
        {
            CountDetail(ref count);
            if (action == null)
                throw new ArgumentException("审计操作不能为 null。", nameof(info));
            actions.Add(new JObject { ["ServiceName"] = Text(action.ServiceName), ["MethodName"] = Text(action.MethodName) });
        }
        foreach (var change in info.EntityChanges ?? Enumerable.Empty<EntityChangeInfo>())
        {
            CountDetail(ref count);
            if (change == null || !Enum.IsDefined(typeof(EntityChangeType), change.ChangeType))
                throw new ArgumentException("实体变更类型无效。", nameof(info));
            var properties = new JArray();
            foreach (var property in change.PropertyChanges ?? Enumerable.Empty<EntityPropertyChangeInfo>())
            {
                CountDetail(ref count);
                if (property == null)
                    throw new ArgumentException("属性变更不能为 null。", nameof(info));
                if (!_allowedProperties.Contains(property.PropertyName ?? string.Empty))
                    continue;
                properties.Add(new JObject
                {
                    ["PropertyName"] = Text(property.PropertyName),
                    ["OriginalValue"] = Text(property.OriginalValue), ["NewValue"] = Text(property.NewValue)
                });
            }
            changes.Add(new JObject
            {
                ["EntityId"] = Text(change.EntityId), ["EntityTenantId"] = Text(change.EntityTenantId),
                ["EntityTypeFullName"] = Text(change.EntityTypeFullName), ["ChangeType"] = change.ChangeType.ToString(),
                ["PropertyChanges"] = properties
            });
        }
        foreach (var exception in info.Exceptions ?? Enumerable.Empty<Exception>())
        {
            CountDetail(ref count);
            if (exception != null)
                exceptions.Add(Text(exception.GetType().FullName));
        }
        var json = new JObject { ["Actions"] = actions, ["EntityChanges"] = changes, ["ExceptionTypes"] = exceptions }
            .ToString(Formatting.None);
        if (Encoding.UTF8.GetByteCount(json) > _maxPayloadBytes)
            throw new InvalidOperationException("审计明细超过 UTF-8 字节上限。");
        return json;
    }
    /// <summary>
    /// 限制明细总数。
    /// </summary>
    /// <param name="count">已计入的明细总数；成功调用后增加一。</param>
    private void CountDetail(ref int count)
    {
        if (count >= _maxDetailCount)
            throw new InvalidOperationException("审计明细超过数量上限。");
        count++;
    }
    /// <summary>
    /// 按长度上限截断文本。
    /// </summary>
    /// <param name="value">待限制长度的文本；为 null 时直接返回 null。</param>
    /// <param name="columnLimit">数据库列允许的最大字符数。</param>
    /// <returns>长度受限且未拆分 UTF-16 代理对的文本；输入为 null 时返回 null。</returns>
    /// <remarks>实际长度上限取全局字段长度与数据库列限制中的较小值，截断时避免拆分 UTF-16 代理对。</remarks>
    private string Text(string value, int columnLimit = int.MaxValue)
    {
        var limit = Math.Min(_maxFieldLength, columnLimit);
        if (value == null || value.Length <= limit)
            return value;
        if (char.IsHighSurrogate(value[limit - 1]))
            limit--;
        return value.Substring(0, limit);
    }
    /// <summary>
    /// 校验并保留租户身份原值。
    /// </summary>
    /// <param name="value">原始租户标识；空值表示非租户审计。</param>
    /// <returns>能够无损编码的原始租户标识，空值返回空字符串。</returns>
    /// <remarks>允许首尾空白以保持与库存身份一致，不执行修剪或 Unicode 规范化。</remarks>
    private static string TenantIdentity(string value)
    {
        return AuditLogTenantIdentity.Validate(value, nameof(value), false);
    }
    /// <summary>
    /// 校验需要保持完整的身份文本。
    /// </summary>
    /// <param name="value">需要校验且不能截断的身份文本。</param>
    /// <param name="name">异常中使用的身份字段参数名。</param>
    /// <param name="optional">允许 null 和空字符串时为 true，否则为 false。</param>
    /// <returns>校验通过的原始身份文本；可选字段输入为 null 时返回 null。</returns>
    private static string Identity(string value, string name, bool optional)
    {
        if (optional && value == null)
            return null;
        if ((string.IsNullOrWhiteSpace(value) && !(optional && value == string.Empty)) || value.Length > 128 || value != value.Trim())
            throw new ArgumentException("审计标识不得超过 128 字符或包含首尾空白。", name);
        return value;
    }
    /// <summary>
    /// 添加数据库参数。
    /// </summary>
    /// <param name="command">接收参数的数据库命令。</param>
    /// <param name="name">数据库参数名称。</param>
    /// <param name="value">待写入的参数值；为 null 时转换为 DBNull.Value。</param>
    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
    /// <summary>
    /// 释放自有资源并收集清理异常。
    /// </summary>
    /// <param name="resource">由存储拥有的待释放资源；为 null 时不执行操作。</param>
    /// <param name="errors">用于收集清理异常的集合。</param>
    private static void DisposeResource(IDisposable resource, ICollection<Exception> errors)
    {
        try { resource?.Dispose(); }
        catch (Exception exception) { errors.Add(exception); }
    }
    /// <summary>
    /// 附加清理异常而不替换原始错误。
    /// </summary>
    /// <param name="failure">保存或清理过程产生的原始异常。</param>
    /// <param name="cleanup">需要附加到原始异常的清理异常。</param>
    private static void AddCleanupError(Exception failure, Exception cleanup)
    {
        const string key = "Bing.AuditCleanupErrors";
        var errors = failure.Data[key] as List<Exception>;
        if (errors == null)
            failure.Data[key] = errors = new List<Exception>();
        errors.Add(cleanup);
    }
}
