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
    /// 初始化一个<see cref="SqlMultipleQueryExecutorBase"/>类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    protected SqlMultipleQueryExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    /// <inheritdoc />
    public ISqlMultipleQueryBatchBuilder CreateBatch() => new SqlMultipleQueryBatchBuilder(Dialect.BatchSeperator);

    /// <inheritdoc />
    public ISqlMultipleQueryResult Execute(SqlMultipleQueryCommand command, int? timeout = null)
    {
        ValidateCommand(command);
        var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var skippedExecution = false;
        try
        {
            if (ExecuteBefore() == false)
            {
                skippedExecution = true;
                return CompleteSkippedExecution(executionLease, cleanupExceptions);
            }
            var connection = GetExecutionConnection();
            var preparedCommand = PrepareCommand(command.Sql, command.Parameters);
            var transaction = GetQueryTransaction();
            message = CreateExecutionDiagnostics(preparedCommand, connection);
            WriteTraceLog(preparedCommand);
            var reader = connection.QueryMultiple(preparedCommand.Sql, preparedCommand.DapperParameters, transaction, timeout);
            return new SqlMultipleQueryResult(reader, executionLease,
                (completed, exception) => CompleteExecution(completed, message, exception),
                (completed, exception) => CompleteExecutionAsync(completed, message, exception));
        }
        catch (Exception) when (skippedExecution)
        {
            throw;
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, primaryException));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return null;
    }

    /// <inheritdoc />
    public async Task<ISqlMultipleQueryResult> ExecuteAsync(SqlMultipleQueryCommand command, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCommand(command);
        EnsureCancellationSupported(cancellationToken);
        var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var skippedExecution = false;
        SqlMapper.GridReader reader = null;
        try
        {
            if (ExecuteBefore() == false)
            {
                skippedExecution = true;
                return CompleteSkippedExecution(executionLease, cleanupExceptions);
            }
            var connection = GetExecutionConnection();
            var preparedCommand = PrepareCommand(command.Sql, command.Parameters);
            var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            message = CreateExecutionDiagnostics(preparedCommand, connection);
            WriteTraceLog(preparedCommand);
            reader = await connection.QueryMultipleAsync(new CommandDefinition(preparedCommand.Sql,
                preparedCommand.DapperParameters, transaction,
                commandTimeout: timeout, cancellationToken: cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return new SqlMultipleQueryResult(reader, executionLease,
                (completed, exception) => CompleteExecution(completed, message, exception),
                (completed, exception) => CompleteExecutionAsync(completed, message, exception));
        }
        catch (Exception) when (skippedExecution)
        {
            throw;
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
            () => SqlTransactionAsyncAdapter.DisposeAsync(reader)).ConfigureAwait(false);
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, RollbackQueryTransactionAsync)
            .ConfigureAwait(false);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, primaryException));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return null;
    }

    /// <summary>
    /// 验证当前命令和 Provider 能力。
    /// </summary>
    /// <param name="command">待执行命令。</param>
    private void ValidateCommand(SqlMultipleQueryCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (GetCurrentProviderProfile().Execution.SupportsMultipleResultSets == false)
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
        var cleanupExceptions = new List<Exception>();
        var lifecycleException = exception;
        if (completed)
        {
            try
            {
                CompleteQueryTransaction();
            }
            catch (Exception completionException)
            {
                cleanupExceptions.Add(completionException);
                lifecycleException ??= completionException;
            }
        }
        else
        {
            try
            {
                RollbackQueryTransaction();
            }
            catch (Exception rollbackException)
            {
                cleanupExceptions.Add(rollbackException);
                lifecycleException ??= rollbackException;
            }
        }
        if (lifecycleException != null)
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, lifecycleException));
        else
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter(message));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupExceptions);
    }

    /// <summary>
    /// 异步完成多结果集执行的事务、诊断和状态清理。
    /// </summary>
    /// <param name="completed">是否完整读取全部结果集。</param>
    /// <param name="message">执行前诊断消息。</param>
    /// <param name="exception">读取过程中的异常。</param>
    private async Task CompleteExecutionAsync(bool completed, DiagnosticsMessage message, Exception exception)
    {
        var cleanupExceptions = new List<Exception>();
        var lifecycleException = exception;
        if (completed)
        {
            try
            {
                await CompleteQueryTransactionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception completionException)
            {
                cleanupExceptions.Add(completionException);
                lifecycleException ??= completionException;
            }
        }
        else
        {
            try
            {
                await RollbackQueryTransactionAsync().ConfigureAwait(false);
            }
            catch (Exception rollbackException)
            {
                cleanupExceptions.Add(rollbackException);
                lifecycleException ??= rollbackException;
            }
        }
        if (lifecycleException != null)
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, lifecycleException));
        else
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter(message));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.ThrowExceptions(exception, cleanupExceptions);
    }

    /// <summary>
    /// 完成被执行前置条件拒绝的多结果集操作。
    /// </summary>
    /// <param name="executionLease">当前执行租约。</param>
    /// <param name="cleanupExceptions">用于保留完成和释放异常的集合。</param>
    /// <returns>始终为 <see langword="null"/>。</returns>
    private ISqlMultipleQueryResult CompleteSkippedExecution(IDisposable executionLease,
        List<Exception> cleanupExceptions)
    {
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(null, cleanupExceptions);
        return null;
    }

}