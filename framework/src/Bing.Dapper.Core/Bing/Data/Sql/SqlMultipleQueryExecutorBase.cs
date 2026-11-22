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
public abstract class SqlMultipleQueryExecutorBase : SqlExecutionBase, ISqlMultipleQueryExecutor
{
    private readonly SqlQueryResourceAccess _queryResources;
    private readonly SqlQueryResourceCleanup _queryResourceCleanup;
    private SqlQueryResourceAccess QueryResources => _queryResources;
    private SqlQueryResourceCleanup QueryResourceCleanup => _queryResourceCleanup;

    /// <inheritdoc />
    private protected override bool IsPrimaryReadTransaction => QueryResources.IsPrimaryReadTransaction;

    /// <summary>获取查询使用的可选事务。</summary>
    protected IDbTransaction GetQueryTransaction() => QueryResources.GetQueryTransaction();
    private protected Task<IDbTransaction> GetQueryTransactionAsync(CancellationToken token = default) => QueryResources.GetQueryTransactionAsync(token);
    /// <summary>完成本次查询拥有的短事务。</summary>
    protected void CompleteQueryTransaction() => QueryResourceCleanup.CompleteQueryTransaction();
    private protected Task CompleteQueryTransactionAsync(CancellationToken token = default) => QueryResourceCleanup.CompleteQueryTransactionAsync(token);
    /// <summary>回滚本次查询拥有的短事务或标记工作单元失败。</summary>
    protected void RollbackQueryTransaction() => QueryResourceCleanup.RollbackQueryTransaction();
    private protected Task RollbackQueryTransactionAsync() => QueryResourceCleanup.RollbackQueryTransactionAsync();

    private void EnsureMultipleResultMode()
    {
        if (ExecutionMode == SqlExecutionMode.GeneratedOnly)
            throw new NotSupportedException("严格生成模式尚不支持 Dapper 多结果集读取。");
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlMultipleQueryExecutorBase"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">服务提供程序。</param>
    /// <param name="options">当前执行器配置。</param>
    protected SqlMultipleQueryExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options, true)
    {
        var owner = CreateQueryResourceOwner();
        _queryResources = new SqlQueryResourceAccess(owner);
        _queryResourceCleanup = new SqlQueryResourceCleanup(owner);
    }

    /// <inheritdoc />
    public ISqlMultipleQueryBatchBuilder CreateBatch() => new SqlMultipleQueryBatchBuilder(Dialect.BatchSeperator);

    /// <inheritdoc />
    public ISqlMultipleQueryResult Execute(SqlMultipleQueryCommand command, int? timeout = null)
    {
        EnsureMultipleResultMode();
        ValidateCommand(command);
        var executionLease = QueryResources.AcquireExecutionLease();
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
            var connection = QueryResources.GetExecutionConnection();
            var preparedCommand = PrepareCommand(command.Sql, command.Parameters);
            var transaction = QueryResources.GetQueryTransaction();
            message = CreateExecutionDiagnostics(preparedCommand, connection);
            WriteTraceLog(preparedCommand);
            var reader = SqlDynamicCommandRunner.QueryMultiple(connection, preparedCommand.Sql, preparedCommand.DapperParameters, transaction, timeout);
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
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransaction);
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
        EnsureMultipleResultMode();
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (cancellationToken.IsCancellationRequested)
        {
            EnsureCancellationSupported(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        ValidateCommand(command);
        var operationCancellation = QueryResources.CreateOperationCancellationLease(cancellationToken);
        var operationToken = operationCancellation.Token;
        try
        {
            EnsureCancellationSupported(operationToken);
            if (operationToken.IsCancellationRequested)
            {
                QueryResources.MarkPreCancelledUnitOfWorkExecution();
                operationToken.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            operationCancellation.Dispose();
            throw;
        }
        IDisposable executionLease;
        try
        {
            executionLease = QueryResources.AcquireExecutionLease();
        }
        catch
        {
            operationCancellation.Dispose();
            throw;
        }
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
                operationCancellation.Dispose();
                operationCancellation = null;
                return CompleteSkippedExecution(executionLease, cleanupExceptions);
            }
            var connection = QueryResources.GetExecutionConnection();
            var preparedCommand = PrepareCommand(command.Sql, command.Parameters);
            var transaction = await QueryResources.GetQueryTransactionAsync(operationToken).ConfigureAwait(false);
            message = CreateExecutionDiagnostics(preparedCommand, connection);
            WriteTraceLog(preparedCommand);
            reader = await SqlDynamicCommandRunner.QueryMultipleAsync(connection, new CommandDefinition(preparedCommand.Sql,
                preparedCommand.DapperParameters, transaction,
                commandTimeout: timeout, cancellationToken: operationToken));
            operationToken.ThrowIfCancellationRequested();
            var result = new SqlMultipleQueryResult(reader, executionLease,
                (completed, exception) => CompleteExecution(completed, message, exception),
                (completed, exception) => CompleteExecutionAsync(completed, message, exception),
                operationToken, operationCancellation);
            operationCancellation = null;
            return result;
        }
        catch (Exception) when (skippedExecution)
        {
            operationCancellation?.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions,
            () => SqlTransactionAsyncAdapter.DisposeAsync(reader)).ConfigureAwait(false);
        await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransactionAsync)
            .ConfigureAwait(false);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, primaryException));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter((object)null));
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => operationCancellation?.Dispose());
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
        if (GetRequiredProviderProfile().Execution.SupportsMultipleResultSets == false)
        {
            var profile = GetRequiredProviderProfile();
            throw SqlCapabilityFailure.Create(profile.Execution.MultipleResultSetsFailureReason ??
                SqlCapabilityFailureReason.DatabaseUnsupported,
                "MultipleResultSets",
                GetCurrentProviderKey(), $"数据库类型 {GetDatabaseType()} 不支持单次命令读取多个结果集。");
        }
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
        completed &= lifecycleException == null;
        if (completed)
        {
            try
            {
                QueryResourceCleanup.CompleteQueryTransaction();
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
                QueryResourceCleanup.RollbackQueryTransaction();
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
        completed &= lifecycleException == null;
        if (completed)
        {
            try
            {
                // 结果集已完整消费，提交属于清理阶段，不继承之后才取消的业务令牌。
                await QueryResourceCleanup.CompleteQueryTransactionAsync(CancellationToken.None).ConfigureAwait(false);
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
                await QueryResourceCleanup.RollbackQueryTransactionAsync().ConfigureAwait(false);
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
