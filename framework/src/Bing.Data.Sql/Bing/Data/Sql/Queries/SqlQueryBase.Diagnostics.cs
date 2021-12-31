using System;
using System.Diagnostics;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql.Queries
{
    // Sql查询对象 - 诊断相关
    public abstract partial class SqlQueryBase
    {
        /// <summary>
        /// 诊断日志
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly DiagnosticListener _diagnosticListener = new DiagnosticListener(SqlQueryDiagnosticListenerNames.DiagnosticListenerName);

        /// <summary>
        /// 执行前诊断
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="parameter">Sql参数</param>
        /// <param name="dataSource">数据源</param>
        protected virtual DiagnosticsMessage ExecuteBefore(string sql, object parameter, string dataSource = null)
        {
            if (!_diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.BeforeExecute))
                return null;
            var message = new DiagnosticsMessage
            {
                Sql = sql,
                Parameters = parameter,
                DataSource = dataSource,
                OperationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Operation = SqlQueryDiagnosticListenerNames.BeforeExecute,
                DatabaseType = SqlOptions.DatabaseType,
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
            if (message?.OperationTimestamp != null && _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.AfterExecute))
            {
                message.Operation = SqlQueryDiagnosticListenerNames.AfterExecute;
                message.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.OperationTimestamp.Value;
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
            if (exception != null && message?.OperationTimestamp != null && _diagnosticListener.IsEnabled(SqlQueryDiagnosticListenerNames.ErrorExecute))
            {
                message.Exception = exception;
                message.Operation = SqlQueryDiagnosticListenerNames.ErrorExecute;
                message.ElapsedMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.OperationTimestamp.Value;

                _diagnosticListener.Write(SqlQueryDiagnosticListenerNames.ErrorExecute, message);
            }
        }
    }
}
