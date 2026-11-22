using System.Data.Common;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Diagnostics;

namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 查询计划的流式执行支持。
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 验证当前查询上下文支持流式读取。
    /// </summary>
    private void EnsureStreamingSupported()
    {
        var profile = GetRequiredProviderProfile();
        if (profile.Execution.SupportsStreaming == false)
            throw SqlCapabilityFailure.Create(profile.Execution.StreamingFailureReason ??
                SqlCapabilityFailureReason.ProviderImplementationGap, "Streaming",
                GetCurrentProviderKey(),
                $"Provider {GetCurrentProvider().Key} 不支持流式查询。");
        var context = GetDatabaseContext();
        if (context?.ReadPreference == SqlReadPreference.Primary &&
            context.DataSource?.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
            throw new InvalidOperationException("PrimaryReadStrategy.Transaction 不支持流式查询。");
    }

    /// <summary>
    /// 以异步流方式执行查询计划，并在枚举终止时完成事务和资源清理。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    private async IAsyncEnumerable<TResult> StreamQueryPlanAsync<TResult>(SqlQueryPlan plan, int? timeout,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        using var operationCancellation = QueryResources.CreateOperationCancellationLease(cancellationToken);
        var operationToken = operationCancellation.Token;
        var isWrite = SqlBuilderRuntimeBridge.ValidateQueryPlan(plan);
        if (operationToken.IsCancellationRequested)
        {
            EnsureCancellationSupported(operationToken);
            QueryResources.MarkPreCancelledUnitOfWorkExecution();
            operationToken.ThrowIfCancellationRequested();
        }
        EnsureStreamingSupported();
        TryCreateStaticReader<TResult>(out var staticReader);
        if (isWrite)
            EnsureWritableDataSource();
        EnsureCancellationSupported(operationToken);
        operationToken.ThrowIfCancellationRequested();
        var executionLease = QueryResources.AcquireExecutionLease();
        DiagnosticsMessage message = default;
        var completed = false;
        IDataReader reader = null;
        DbCommand staticCommand = null;
        IDbConnection staticConnection = null;
        var closeStaticConnection = false;
        Func<IDataReader, TResult> parser = null;
        IDisposable logScope = null;
        var shouldExecute = false;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var queryExecutionStarted = false;
        try
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (shouldExecute = ExecuteBefore())
            {
                try
                {
                    var preparedCommand = PrepareQueryPlan(plan);
                    var connection = QueryResources.GetExecutionConnection();
                    var transaction = await QueryResources.GetQueryTransactionAsync(operationToken).ConfigureAwait(false);
                    message = CreateExecutionDiagnostics(preparedCommand, connection, plan);
                    logScope = BeginExecutionLogScope(message);
                    WritePlanTraceLog(preparedCommand);
                    if (staticReader != null && CanExecuteStatically(preparedCommand.DapperParameters))
                    {
                        staticCommand = SqlStaticCommandRunner.CreateCommand(connection, preparedCommand.Sql,
                            preparedCommand.DapperParameters, transaction, timeout, plan.CommandType);
                        staticConnection = connection;
                        closeStaticConnection = connection.State != ConnectionState.Open;
                        if (closeStaticConnection) await ((DbConnection)connection).OpenAsync(operationToken).ConfigureAwait(false);
                        reader = await staticCommand.ExecuteReaderAsync(operationToken).ConfigureAwait(false);
                        var read = staticReader.Bind((DbDataReader)reader);
                        parser = _ => read();
                    }
                    else
                    {
                        reader = await SqlDynamicCommandRunner.ExecuteReaderAsync(connection, CreateQueryCommandDefinition(preparedCommand.Sql,
                            preparedCommand.DapperParameters, transaction, timeout, buffered: false, operationToken,
                            plan.CommandType));
                        parser = SqlDynamicCommandRunner.GetRowParser<TResult>(reader);
                    }
                }
                catch (Exception exception)
                {
                    primaryException = exception;
                    if (reader != null)
                    {
                        try
                        {
                            await SqlTransactionAsyncAdapter.DisposeAsync(reader).ConfigureAwait(false);
                        }
                        catch (Exception cleanupException)
                        {
                            cleanupExceptions.Add(cleanupException);
                        }
                        finally
                        {
                            reader = null;
                        }
                    }
                }
                if (primaryException == null)
                {
                    try
                    {
                        while (true)
                        {
                            TResult item;
                            try
                            {
                                if (reader is DbDataReader dbReader)
                                {
                                    if (await dbReader.ReadAsync(operationToken) == false)
                                        break;
                                    item = parser(dbReader);
                                }
                                else
                                {
                                    operationToken.ThrowIfCancellationRequested();
                                    if (reader.Read() == false)
                                        break;
                                    item = parser(reader);
                                }
                            }
                            catch (Exception exception)
                            {
                                primaryException = exception;
                                break;
                            }
                            yield return item;
                        }
                    }
                    finally
                    {
                        try
                        {
                            await SqlTransactionAsyncAdapter.DisposeAsync(reader).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            if (primaryException == null)
                                primaryException = exception;
                            else
                                cleanupExceptions.Add(exception);
                        }
                    }
                    if (primaryException == null)
                    {
                        try
                        {
                            QueryResourceCleanup.CompleteQueryTransaction();
                            completed = true;
                        }
                        catch (Exception exception)
                        {
                            primaryException = exception;
                        }
                    }
                }
            }
        }
        finally
        {
            if (primaryException != null || shouldExecute && completed == false)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransaction);
            if (primaryException != null)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, primaryException));
            else
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter(message));
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteQueryPlanAfter(null));
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => logScope?.Dispose());
            if (queryExecutionStarted)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, plan.NotifyExecutionFinished);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => staticCommand?.Dispose());
            if (closeStaticConnection)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, staticConnection.Close);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
            SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        }
    }

    /// <summary>
    /// 以同步流方式执行查询计划，并在枚举终止时完成事务和资源清理。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    private IEnumerable<TResult> StreamQueryPlan<TResult>(SqlQueryPlan plan, int? timeout)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        EnsureStreamingSupported();
        TryCreateStaticReader<TResult>(out var staticReader);
        if (SqlBuilderRuntimeBridge.ValidateQueryPlan(plan))
            EnsureWritableDataSource();
        var executionLease = QueryResources.AcquireExecutionLease();
        DiagnosticsMessage message = default;
        var completed = false;
        IEnumerator<TResult> enumerator = null;
        IDisposable logScope = null;
        var shouldExecute = false;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var queryExecutionStarted = false;
        try
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (shouldExecute = ExecuteBefore())
            {
                try
                {
                    var preparedCommand = PrepareQueryPlan(plan);
                    var connection = QueryResources.GetExecutionConnection();
                    var transaction = QueryResources.GetQueryTransaction();
                    message = CreateExecutionDiagnostics(preparedCommand, connection, plan);
                    logScope = BeginExecutionLogScope(message);
                    WritePlanTraceLog(preparedCommand);
                    enumerator = staticReader != null && CanExecuteStatically(preparedCommand.DapperParameters)
                        ? StreamStaticRows(connection, preparedCommand.Sql, preparedCommand.DapperParameters, transaction,
                            timeout, staticReader, plan.CommandType).GetEnumerator()
                        : SqlDynamicCommandRunner.Query<TResult>(connection, preparedCommand.Sql, preparedCommand.DapperParameters, transaction,
                            buffered: false, commandTimeout: timeout, commandType: plan.CommandType).GetEnumerator();
                }
                catch (Exception exception)
                {
                    primaryException = exception;
                }
                if (primaryException == null)
                {
                    try
                    {
                        while (true)
                        {
                            TResult item;
                            try
                            {
                                if (enumerator.MoveNext() == false)
                                    break;
                                item = enumerator.Current;
                            }
                            catch (Exception exception)
                            {
                                primaryException = exception;
                                break;
                            }
                            yield return item;
                        }
                    }
                    finally
                    {
                        try
                        {
                            enumerator?.Dispose();
                        }
                        catch (Exception exception)
                        {
                            if (primaryException == null)
                                primaryException = exception;
                            else
                                cleanupExceptions.Add(exception);
                        }
                    }
                    if (primaryException == null)
                    {
                        try
                        {
                            QueryResourceCleanup.CompleteQueryTransaction();
                            completed = true;
                        }
                        catch (Exception exception)
                        {
                            primaryException = exception;
                        }
                    }
                }
            }
        }
        finally
        {
            if (primaryException != null || shouldExecute && completed == false)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransaction);
            if (primaryException != null)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, primaryException));
            else
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteAfter(message));
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteQueryPlanAfter(null));
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => logScope?.Dispose());
            if (queryExecutionStarted)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, plan.NotifyExecutionFinished);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
            SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        }
    }
}
