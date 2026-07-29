using Dapper;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper 多结果集查询执行器基类。
/// </summary>
/// <remarks>
/// 结果读取期间会持有当前实例的执行租约，调用方必须释放返回的结果对象。
/// </remarks>
public abstract class SqlMultipleQueryExecutorBase : SqlQueryBase, ISqlMultipleQueryExecutor
{
    /// <summary>
    /// 当前 Provider 的可选运行能力。
    /// </summary>
    private readonly SqlProviderCapabilities _capabilities;

    /// <summary>
    /// 初始化一个<see cref="SqlMultipleQueryExecutorBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    /// <param name="capabilities">当前 Provider 的运行能力。</param>
    protected SqlMultipleQueryExecutorBase(IServiceProvider serviceProvider, SqlOptions options,
        SqlProviderCapabilities capabilities)
        : base(serviceProvider, options)
    {
        _capabilities = capabilities ?? new SqlProviderCapabilities();
    }

    /// <inheritdoc />
    public ISqlMultipleQueryBatchBuilder CreateBatch() => new SqlMultipleQueryBatchBuilder(Dialect.BatchSeperator);

    /// <inheritdoc />
    public ISqlMultipleQueryResult Execute(SqlMultipleQueryCommand command, int? timeout = null)
    {
        ValidateCommand(command);
        var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        try
        {
            if (ExecuteBefore() == false)
            {
                executionLease.Dispose();
                ExecuteAfter((object)null);
                return null;
            }
            var connection = GetExecutionConnection();
            var dbParameters = GetDbParameters(command.Parameters, command.Sql);
            var parameterMetadata = GetSqlParameterDiagnostics(command.Parameters, command.Sql);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(command.Sql, command.Parameters, connection, parameterMetadata);
            WriteTraceLog(command.Sql, ToParameterValues(command.Parameters), command.Sql);
            var reader = connection.QueryMultiple(command.Sql, dbParameters, transaction, timeout);
            return new SqlMultipleQueryResult(reader, executionLease,
                (completed, exception) => CompleteExecution(completed, message, exception));
        }
        catch (Exception exception)
        {
            executionLease.Dispose();
            RollbackQueryTransaction();
            ExecuteError(message, exception);
            ExecuteAfter((object)null);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ISqlMultipleQueryResult> ExecuteAsync(SqlMultipleQueryCommand command, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);
        var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        try
        {
            if (ExecuteBefore() == false)
            {
                executionLease.Dispose();
                ExecuteAfter((object)null);
                return null;
            }
            var connection = GetExecutionConnection();
            var dbParameters = GetDbParameters(command.Parameters, command.Sql);
            var parameterMetadata = GetSqlParameterDiagnostics(command.Parameters, command.Sql);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(command.Sql, command.Parameters, connection, parameterMetadata);
            WriteTraceLog(command.Sql, ToParameterValues(command.Parameters), command.Sql);
            var reader = await connection.QueryMultipleAsync(new CommandDefinition(command.Sql, dbParameters, transaction,
                commandTimeout: timeout, cancellationToken: cancellationToken));
            return new SqlMultipleQueryResult(reader, executionLease,
                (completed, exception) => CompleteExecution(completed, message, exception));
        }
        catch (Exception exception)
        {
            executionLease.Dispose();
            RollbackQueryTransaction();
            ExecuteError(message, exception);
            ExecuteAfter((object)null);
            throw;
        }
    }

    /// <summary>
    /// 验证当前命令和 Provider 能力。
    /// </summary>
    /// <param name="command">待执行命令。</param>
    private void ValidateCommand(SqlMultipleQueryCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (_capabilities.SupportsMultipleResultSets == false)
            throw new NotSupportedException($"数据库类型 {GetDatabaseType()} 不支持单次命令读取多个结果集。");
    }

    /// <summary>
    /// 完成多结果集执行的事务、诊断和状态清理。
    /// </summary>
    /// <param name="completed">是否完整读取全部结果集。</param>
    /// <param name="message">执行前诊断消息。</param>
    /// <param name="exception">读取过程中的异常。</param>
    private void CompleteExecution(bool completed, DiagnosticsMessage message, Exception exception)
    {
        try
        {
            if (completed)
                CompleteQueryTransaction();
            else
                RollbackQueryTransaction();
            if (exception != null)
                ExecuteError(message, exception);
            else
                ExecuteAfter(message);
        }
        finally
        {
            ExecuteAfter((object)null);
        }
    }

    /// <summary>
    /// 将参数快照转换为诊断日志使用的键值集合。
    /// </summary>
    /// <param name="parameters">参数快照。</param>
    /// <returns>参数键值集合。</returns>
    private static IReadOnlyDictionary<string, object> ToParameterValues(IEnumerable<Builders.Params.SqlParam> parameters)
    {
        return (parameters ?? Array.Empty<Builders.Params.SqlParam>())
            .Where(parameter => parameter != null)
            .ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.OrdinalIgnoreCase);
    }
}