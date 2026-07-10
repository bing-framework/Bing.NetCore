using System.Diagnostics;
using Bing.Data.Sql.Builders;
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
    /// <param name="boundParameters">绑定后的 Sql 参数</param>
    /// <param name="parameterMetadata">Sql 增强参数元数据</param>
    protected virtual DiagnosticsMessage ExecuteBefore(string sql, object parameter, IDbConnection connection,
        object boundParameters = null, IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterMetadata = null)
    {
        if (!_diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.BeforeExecute))
            return null;
        var parameterSnapshot = CreateParameterSnapshot(parameter, boundParameters, parameterMetadata);
        var connectionInfo = CreateConnectionDiagnosticInfo(connection);
        var message = new DiagnosticsMessage
        {
            Sql = sql,
            Parameters = parameter,
            RawParameters = parameter,
            BoundParameters = boundParameters,
            SqlParametersMetadata = parameterSnapshot.Items,
            ParameterSnapshot = parameterSnapshot,
            Connection = connectionInfo,
            Transaction = CreateTransactionDiagnosticInfo(GetTransaction()),
            Database = connectionInfo.Database,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Operation = SqlQueryDiagnosticListenerNames.BeforeExecute,
            DatabaseType = connectionInfo.DatabaseType,
        };
        _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.BeforeExecute, message);
        return message;
    }

    /// <summary>
    /// 创建参数诊断快照
    /// </summary>
    /// <param name="parameter">原始参数</param>
    /// <param name="boundParameters">绑定后的参数</param>
    /// <param name="parameterMetadata">参数元数据</param>
    /// <returns>参数诊断快照</returns>
    protected virtual SqlParameterDiagnosticSnapshot CreateParameterSnapshot(object parameter, object boundParameters,
        IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterMetadata)
    {
        return new SqlParameterDiagnosticSnapshot
        {
            RawParameters = parameter,
            BoundParameters = boundParameters,
            Items = parameterMetadata?.Where(t => t != null).ToList() ?? CreateParameterDiagnosticItems(parameter)
        };
    }

    /// <summary>
    /// 创建参数诊断项
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <returns>参数诊断项集合</returns>
    private IReadOnlyCollection<SqlParameterDiagnosticInfo> CreateParameterDiagnosticItems(object parameter)
    {
        if (parameter is IReadOnlyDictionary<string, object> dict)
        {
            return dict.Select(t => new SqlParameterDiagnosticInfo
            {
                Name = t.Key,
                Value = t.Value,
                OriginalValue = t.Value,
                IsSensitive = IsSensitiveParameter(t.Key)
            }).ToList();
        }
        return new List<SqlParameterDiagnosticInfo>();
    }

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
            DataSource = GetDataSource(connection),
            DataSourceKey = context?.DataSourceKey ?? context?.DbKey,
            DatabaseType = context?.DatabaseType ?? Options.DatabaseType,
            State = connection?.State ?? ConnectionState.Closed,
            ConnectionType = connection?.GetType().FullName
        };
    }

    /// <summary>
    /// 获取数据源名称
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>数据源名称</returns>
    private static string GetDataSource(IDbConnection connection)
    {
        if (connection == null)
            return null;
        var property = connection.GetType().GetProperty("DataSource");
        return property?.GetValue(connection)?.ToString();
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
            IsolationLevel = GetIsolationLevel(transaction),
            TransactionType = transaction.GetType().FullName
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
    {
        if (SqlParameterBinder is not ISqlParameterContextBinder binder)
            return new List<SqlParameterDiagnosticInfo>();
        return binder.GetSqlParams(builder, Options).Select(CreateSqlParameterDiagnosticInfo).ToList();
    }

    /// <summary>
    /// 获取 Sql 参数诊断元数据
    /// </summary>
    /// <param name="parameter">Sql 参数对象</param>
    /// <returns>Sql 参数诊断元数据</returns>
    protected virtual IReadOnlyCollection<SqlParameterDiagnosticInfo> GetSqlParameterDiagnostics(object parameter)
    {
        if (SqlParameterBinder is not ISqlParameterContextBinder binder)
            return new List<SqlParameterDiagnosticInfo>();
        return binder.GetSqlParams(parameter, Options).Select(CreateSqlParameterDiagnosticInfo).ToList();
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
            Value = parameter.Value,
            OriginalValue = parameter.Value,
            IsSensitive = IsSensitiveParameter(parameter.Name),
            DbType = parameter.DbType,
            Direction = parameter.Direction,
            Size = parameter.Size,
            Precision = parameter.Precision,
            Scale = parameter.Scale,
            EntityType = parameter.EntityType?.FullName,
            PropertyName = parameter.PropertyName,
            ColumnName = parameter.ColumnName,
            DatabaseType = parameter.DatabaseType,
            DatabaseRole = parameter.DatabaseRole,
            ProviderTypeName = parameter.ProviderTypeName,
            Source = parameter.Source,
            MetadataLevel = parameter.MetadataLevel,
            StorageKind = parameter.StorageKind,
            ConverterKind = parameter.ConverterKind,
            CustomConverterName = parameter.CustomConverterName
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
               name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
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
            message.Operation = SqlQueryDiagnosticListenerNames.AfterExecute;
            message.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.Timestamp.Value;
            _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.AfterExecute, message);
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
            message.Exception = exception;
            message.Operation = SqlQueryDiagnosticListenerNames.ErrorExecute;
            message.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.Timestamp.Value;

            _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.ErrorExecute, message);
        }
    }
}
