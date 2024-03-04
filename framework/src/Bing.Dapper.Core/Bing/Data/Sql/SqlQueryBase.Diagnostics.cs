using System.Diagnostics;
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
    protected virtual DiagnosticsMessage ExecuteBefore(string sql, object parameter, IDbConnection connection)
    {
        if (!_diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.BeforeExecute))
            return null;
        var message = new DiagnosticsMessage
        {
            Sql = sql,
            Parameters = parameter,
            Database = connection.Database,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Operation = SqlQueryDiagnosticListenerNames.BeforeExecute,
            DatabaseType = Options.DatabaseType,
        };
        _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.BeforeExecute, message);
        return message;
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
