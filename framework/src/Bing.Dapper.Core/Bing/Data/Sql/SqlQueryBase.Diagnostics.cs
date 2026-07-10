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
        var message = new DiagnosticsMessage
        {
            Sql = sql,
            Parameters = parameter,
            RawParameters = parameter,
            BoundParameters = boundParameters,
            SqlParametersMetadata = parameterMetadata,
            Database = connection.Database,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Operation = SqlQueryDiagnosticListenerNames.BeforeExecute,
            DatabaseType = Options.DatabaseType,
        };
        _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.BeforeExecute, message);
        return message;
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
