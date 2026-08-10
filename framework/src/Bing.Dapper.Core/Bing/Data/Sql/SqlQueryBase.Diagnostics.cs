using System.Diagnostics;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

// Sql查询对象 - 诊断相关
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 诊断日志
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly DiagnosticListener _diagnosticListener = new(SqlQueryDiagnosticListenerNames.DiagnosticListenerName);

    /// <summary>
    /// 执行前诊断
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="parameter">Sql参数</param>
    /// <param name="connection">数据库连接</param>
    /// <param name="parameterMetadata">Sql 增强参数元数据</param>
    protected virtual DiagnosticsMessage ExecuteBefore(string sql, object parameter, IDbConnection connection,
        IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterMetadata = null)
    {
        if (IsExecutionDiagnosticsEnabled() == false)
            return null;
        var parameters = CreateParameterDiagnostics(parameter, parameterMetadata);
        var connectionInfo = CreateConnectionDiagnosticInfo(connection);
        var context = Options.GetDatabaseContext();
        var message = new DiagnosticsMessage
        {
            Sql = sql,
            MappingProfile = context?.MappingProfile,
            TenantId = Options.IncludeTenantIdInDiagnostics ? context?.TenantId : null,
            Parameters = parameters,
            Connection = connectionInfo,
            Transaction = CreateTransactionDiagnosticInfo(GetExecutionTransaction()),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Operation = SqlQueryDiagnosticListenerNames.BeforeExecute
        };
        if (_diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.BeforeExecute))
            _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.BeforeExecute, CloneDiagnosticsMessage(message));
        return message;
    }

    /// <summary>
    /// 为 Builder 执行按需创建执行前诊断消息。
    /// </summary>
    /// <param name="builder">当前 SQL Builder。</param>
    /// <param name="sql">当前执行的 SQL 文本。</param>
    /// <param name="dapperParameters">本次执行已绑定的 Dapper 参数。</param>
    /// <param name="connection">当前数据库连接。</param>
    /// <returns>监听器未启用时返回 null；否则返回执行前诊断消息。</returns>
    private protected DiagnosticsMessage CreateExecutionDiagnostics(ISqlBuilder builder, string sql,
        object dapperParameters, IDbConnection connection)
    {
        if (IsExecutionDiagnosticsEnabled() == false)
            return null;
        var parameters = builder?.GetParams();
        return ExecuteBefore(sql, parameters, connection, GetBoundParameterDiagnostics(dapperParameters,
            () => GetSqlParameterDiagnostics(builder, sql)));
    }

    /// <summary>
    /// 为原生参数执行按需创建执行前诊断消息。
    /// </summary>
    /// <param name="sql">当前执行的 SQL 文本。</param>
    /// <param name="parameters">调用方提供的原始参数。</param>
    /// <param name="dapperParameters">本次执行已绑定的 Dapper 参数。</param>
    /// <param name="connection">当前数据库连接。</param>
    /// <returns>监听器未启用时返回 null；否则返回执行前诊断消息。</returns>
    private protected DiagnosticsMessage CreateExecutionDiagnostics(string sql, object parameters,
        object dapperParameters, IDbConnection connection)
    {
        if (IsExecutionDiagnosticsEnabled() == false)
            return null;
        return ExecuteBefore(sql, parameters, connection, GetBoundParameterDiagnostics(dapperParameters,
            () => GetSqlParameterDiagnostics(parameters, sql)));
    }

    /// <summary>
    /// 为已准备命令按需创建执行前诊断消息。
    /// </summary>
    /// <param name="command">当前执行的准备命令。</param>
    /// <param name="connection">当前数据库连接。</param>
    /// <returns>监听器未启用时返回 null；否则返回执行前诊断消息。</returns>
    private protected DiagnosticsMessage CreateExecutionDiagnostics(SqlPreparedCommand command,
        IDbConnection connection)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        return command.IsBuilderCommand
            ? CreateExecutionDiagnostics(command.Builder, command.Sql, command.DapperParameters, connection)
            : CreateExecutionDiagnostics(command.Sql, command.ParameterSource, command.DapperParameters, connection);
    }

    /// <summary>
    /// 判断任一执行生命周期诊断事件是否已启用。
    /// </summary>
    /// <returns>存在启用的执行诊断事件时返回 <see langword="true"/>。</returns>
    private static bool IsExecutionDiagnosticsEnabled() =>
        _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.BeforeExecute) ||
        _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.AfterExecute) ||
        _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.ErrorExecute);

    /// <summary>
    /// 优先从本次已绑定的参数集创建诊断元数据，避免为诊断再次绑定参数源。
    /// </summary>
    /// <param name="dapperParameters">本次执行已绑定的 Dapper 参数。</param>
    /// <param name="fallback">未提供增强参数集时的诊断元数据工厂。</param>
    /// <returns>当前执行的参数诊断元数据。</returns>
    private IReadOnlyCollection<SqlParameterDiagnosticInfo> GetBoundParameterDiagnostics(object dapperParameters,
        Func<IReadOnlyCollection<SqlParameterDiagnosticInfo>> fallback)
    {
        if (dapperParameters is IDapperParameterSet parameterSet)
            return parameterSet.Parameters.Select(CreateSqlParameterDiagnosticInfo).ToList();
        return fallback();
    }

    /// <summary>
    /// 创建参数诊断快照
    /// </summary>
    /// <param name="parameter">原始参数</param>
    /// <param name="parameterMetadata">参数元数据</param>
    /// <returns>参数诊断快照</returns>
    protected virtual SqlParameterDiagnosticSnapshot CreateParameterDiagnostics(object parameter,
        IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterMetadata)
    {
        var metadata = parameterMetadata?.Where(t => t != null).ToList();
        return new SqlParameterDiagnosticSnapshot
        {
            OriginalParameterType = parameter?.GetType().FullName,
            IsMetadataBound = metadata?.Count > 0,
            Items = metadata?.Count > 0 ? metadata : CreateParameterDiagnosticItems(parameter).ToList()
        };
    }

    /// <summary>
    /// 创建参数诊断项
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <returns>参数诊断项集合</returns>
    private IReadOnlyCollection<SqlParameterDiagnosticInfo> CreateParameterDiagnosticItems(object parameter)
    {
        if (parameter == null)
            return Array.Empty<SqlParameterDiagnosticInfo>();
        if (parameter is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return CreateParameterDiagnosticItems(readOnlyDictionary);
        if (parameter is IDictionary<string, object> dictionary)
            return CreateParameterDiagnosticItems(dictionary);
        return parameter.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(t => t.CanRead && t.GetIndexParameters().Length == 0)
            .Select(t =>
            {
                var value = t.GetValue(parameter);
                var isSensitive = IsSensitiveParameter(t.Name);
                return new SqlParameterDiagnosticInfo
                {
                    Name = t.Name,
                    Value = isSensitive ? null : value,
                    IsSensitive = isSensitive
                };
            })
            .ToList();
    }

    /// <summary>
    /// 根据字典参数创建诊断项。
    /// </summary>
    /// <param name="parameters">参数字典。</param>
    /// <returns>按参数名生成的诊断项集合。</returns>
    private static IReadOnlyCollection<SqlParameterDiagnosticInfo> CreateParameterDiagnosticItems(
        IEnumerable<KeyValuePair<string, object>> parameters) => parameters.Select(item => new SqlParameterDiagnosticInfo
    {
        Name = item.Key,
        Value = IsSensitiveParameter(item.Key) ? null : item.Value,
        IsSensitive = IsSensitiveParameter(item.Key)
    }).ToList();

    /// <summary>
    /// 创建连接诊断信息
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>连接诊断信息</returns>
    protected virtual SqlConnectionDiagnosticInfo CreateConnectionDiagnosticInfo(IDbConnection connection)
    {
        var context = Options.GetDatabaseContext();
        return new SqlConnectionDiagnosticInfo
        {
            Database = connection?.Database,
            DbKey = context?.DataSource?.Key ?? context?.DbKey,
            DatabaseType = context?.DataSource?.DatabaseType ?? Options.DatabaseType,
            Source = ConnectionSource != SqlConnectionSource.Unknown
                ? ConnectionSource
                : _connectionOwnership == SqlResourceOwnership.External
                ? SqlConnectionSource.External
                : SqlConnectionSource.DataSource,
            Ownership = _connectionOwnership,
            IsReadOnly = context?.DataSource?.IsReadOnly ?? false,
            ReadPreference = context?.ReadPreference ?? SqlReadPreference.Default
        };
    }

    /// <summary>
    /// 创建事务诊断信息
    /// </summary>
    /// <param name="transaction">数据库事务</param>
    /// <returns>事务诊断信息</returns>
    protected virtual SqlTransactionDiagnosticInfo CreateTransactionDiagnosticInfo(IDbTransaction transaction)
    {
        if (transaction == null)
            return new SqlTransactionDiagnosticInfo();
        return new SqlTransactionDiagnosticInfo
        {
            HasTransaction = true,
            TransactionId = _transactionId ?? transaction.GetHashCode().ToString("X"),
            IsolationLevel = GetIsolationLevel(transaction),
            Ownership = _transaction == null ? SqlResourceOwnership.External : _transactionOwnership,
            IsPrimaryReadTransaction = _primaryReadTransactionStarted
        };
    }

    /// <summary>
    /// 获取事务隔离级别
    /// </summary>
    /// <param name="transaction">数据库事务</param>
    /// <returns>事务隔离级别</returns>
    private static IsolationLevel? GetIsolationLevel(IDbTransaction transaction)
    {
        try
        {
            return transaction?.IsolationLevel;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取 Sql 参数诊断元数据
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <returns>Sql 参数诊断元数据</returns>
    protected virtual IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(ISqlBuilder builder)
        => GetSqlParameterDiagnostics(builder, null);

    /// <summary>
    /// 获取 Sql 参数诊断元数据
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="sql">当前执行的 Sql 语句</param>
    /// <returns>Sql 参数诊断元数据</returns>
    protected virtual IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(ISqlBuilder builder,
        string sql)
    {
        if (SqlParameterBinder is not ISqlParameterContextBinder binder)
            return new List<SqlParameterDiagnosticInfo>();
        return binder.GetSqlParams(builder, Options, CreateParameterBindingContext(sql, builder?.GetParams()))
            .Select(CreateSqlParameterDiagnosticInfo).ToList();
    }

    /// <summary>
    /// 获取 Sql 参数诊断元数据
    /// </summary>
    /// <param name="parameter">Sql 参数对象</param>
    /// <returns>Sql 参数诊断元数据</returns>
    protected virtual IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(object parameter)
        => GetSqlParameterDiagnostics(parameter, null);

    /// <summary>
    /// 获取 Sql 参数诊断元数据
    /// </summary>
    /// <param name="parameter">Sql 参数对象</param>
    /// <param name="sql">当前执行的 Sql 语句</param>
    /// <returns>Sql 参数诊断元数据</returns>
    protected virtual IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(object parameter,
        string sql)
    {
        if (SqlParameterBinder is not ISqlParameterContextBinder binder)
            return new List<SqlParameterDiagnosticInfo>();
        return binder.GetSqlParams(parameter, Options, CreateParameterBindingContext(sql, parameter))
            .Select(CreateSqlParameterDiagnosticInfo).ToList();
    }

    /// <summary>
    /// 创建 Sql 参数诊断信息
    /// </summary>
    /// <param name="parameter">Sql 增强参数</param>
    /// <returns>Sql 参数诊断信息</returns>
    protected virtual SqlParameterDiagnosticInfo CreateSqlParameterDiagnosticInfo(SqlParam parameter)
    {
        if (parameter == null)
            return null;
        return new SqlParameterDiagnosticInfo
        {
            Name = parameter.Name,
            Value = IsSensitiveParameter(parameter.Name) ? null : parameter.Value,
            OriginalValue = IsSensitiveParameter(parameter.Name) ? null : parameter.OriginalValue,
            IsSensitive = IsSensitiveParameter(parameter.Name),
            DbType = parameter.DbType,
            Direction = parameter.Direction,
            Size = parameter.Size,
            Precision = parameter.Precision,
            Scale = parameter.Scale,
            EntityType = parameter.EntityType?.FullName,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel
        };
    }

    /// <summary>
    /// 是否敏感参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <returns>是返回 true，否则返回 false</returns>
    private static bool IsSensitiveParameter(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("passphrase", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("authorization", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("signature", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 执行后诊断
    /// </summary>
    /// <param name="message">诊断消息</param>
    protected virtual void ExecuteAfter(DiagnosticsMessage message)
    {
        if (!_diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.AfterExecute))
            return;
        if (message?.Timestamp != null)
        {
            var snapshot = CloneDiagnosticsMessage(message);
            snapshot.Operation = SqlQueryDiagnosticListenerNames.AfterExecute;
            snapshot.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - snapshot.Timestamp.Value;
            _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.AfterExecute, snapshot);
        }
    }

    /// <summary>
    /// 执行异常诊断
    /// </summary>
    /// <param name="message">诊断消息</param>
    /// <param name="exception">异常</param>
    protected virtual void ExecuteError(DiagnosticsMessage message, Exception exception)
    {
        if (exception != null && message?.Timestamp != null && _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.ErrorExecute))
        {
            var snapshot = CloneDiagnosticsMessage(message);
            snapshot.Exception = exception;
            snapshot.Operation = SqlQueryDiagnosticListenerNames.ErrorExecute;
            snapshot.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - snapshot.Timestamp.Value;

            _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.ErrorExecute, snapshot);
        }
    }

    /// <summary>
    /// 创建互不共享可变对象的诊断消息副本。
    /// </summary>
    /// <param name="message">原始诊断消息。</param>
    /// <returns>诊断消息副本。</returns>
    private static DiagnosticsMessage CloneDiagnosticsMessage(DiagnosticsMessage message)
    {
        if (message == null)
            return null;
        return new DiagnosticsMessage
        {
            Timestamp = message.Timestamp,
            Operation = message.Operation,
            OperationId = message.OperationId,
            Sql = message.Sql,
            MappingProfile = message.MappingProfile,
            TenantId = message.TenantId,
            Parameters = message.Parameters == null
                ? null
                : new SqlParameterDiagnosticSnapshot
                {
                    OriginalParameterType = message.Parameters.OriginalParameterType,
                    IsMetadataBound = message.Parameters.IsMetadataBound,
                    Items = message.Parameters.Items.Select(CloneSqlParameterDiagnosticInfo).ToList()
                },
            Connection = message.Connection == null
                ? null
                : new SqlConnectionDiagnosticInfo
                {
                    Database = message.Connection.Database,
                    DbKey = message.Connection.DbKey,
                    DatabaseType = message.Connection.DatabaseType,
                    Source = message.Connection.Source,
                    Ownership = message.Connection.Ownership,
                    IsReadOnly = message.Connection.IsReadOnly,
                    ReadPreference = message.Connection.ReadPreference
                },
            Transaction = message.Transaction == null
                ? null
                : new SqlTransactionDiagnosticInfo
                {
                    TransactionId = message.Transaction.TransactionId,
                    HasTransaction = message.Transaction.HasTransaction,
                    IsolationLevel = message.Transaction.IsolationLevel,
                    Ownership = message.Transaction.Ownership,
                    IsPrimaryReadTransaction = message.Transaction.IsPrimaryReadTransaction
                },
            ElapsedMilliseconds = message.ElapsedMilliseconds,
            Exception = message.Exception
        };
    }

    /// <summary>
    /// 创建 SQL 参数诊断信息副本。
    /// </summary>
    /// <param name="parameter">原始 SQL 参数诊断信息。</param>
    /// <returns>SQL 参数诊断信息副本。</returns>
    private static SqlParameterDiagnosticInfo CloneSqlParameterDiagnosticInfo(SqlParameterDiagnosticInfo parameter)
    {
        if (parameter == null)
            return null;
        return new SqlParameterDiagnosticInfo
        {
            Name = parameter.Name,
            Value = CloneDiagnosticValue(parameter.Value),
            OriginalValue = CloneDiagnosticValue(parameter.OriginalValue),
            IsSensitive = parameter.IsSensitive,
            DbType = parameter.DbType,
            Direction = parameter.Direction,
            Size = parameter.Size,
            Precision = parameter.Precision,
            Scale = parameter.Scale,
            EntityType = parameter.EntityType,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel
        };
    }

    /// <summary>
    /// 复制诊断中常见的可变参数值，避免观察器修改影响其它诊断快照。
    /// </summary>
    /// <param name="value">原始参数值。</param>
    /// <returns>可安全暴露给诊断观察器的参数值。</returns>
    private static object CloneDiagnosticValue(object value) => value is Array array && array.Rank == 1
        ? array.Clone()
        : value;
}
