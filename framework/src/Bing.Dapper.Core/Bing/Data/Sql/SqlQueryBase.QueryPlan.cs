using Bing.Helpers;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 查询计划的构建和执行支持。
/// </summary>
public abstract partial class SqlQueryBase
{
    /// <summary>
    /// 使用查询计划执行同步查询，并复用根查询的连接、事务、参数和诊断生命周期。
    /// </summary>
    /// <typeparam name="TResult">执行结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="operation">调用 Dapper 查询 API 的操作。</param>
    /// <param name="acquireExecutionLease">是否为当前内部计划单独获取执行租约。</param>
    /// <param name="completeTransaction">当前内部计划成功后是否完成事务。</param>
    /// <returns>最终执行结果。</returns>
    private TResult InternalQueryPlan<TResult>(SqlQueryPlan plan,
        Func<IDbConnection, string, object, IDbTransaction, TResult> operation, bool acquireExecutionLease = true,
        bool completeTransaction = true)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        var executionLease = acquireExecutionLease ? QueryResources.AcquireExecutionLease() : null;
        TResult result = default;
        DiagnosticsMessage message = default;
        var queryExecutionStarted = false;
        return SqlQueryPlanLifecycle.Execute(() =>
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (ExecuteBefore())
            {
                var preparedCommand = PrepareQueryPlan(plan);
                var connection = QueryResources.GetExecutionConnection();
                var transaction = QueryResources.GetQueryTransaction();
                message = CreateExecutionDiagnostics(preparedCommand, connection, plan);
                using var logScope = BeginExecutionLogScope(message);
                WritePlanTraceLog(preparedCommand);
                result = operation(connection, preparedCommand.Sql, preparedCommand.DapperParameters, transaction);
                plan.NotifyExecutionCompleted();
                if (completeTransaction)
                    QueryResourceCleanup.CompleteQueryTransaction();
                ExecuteAfter(message);
            }
            return result;
        }, (exception, cleanupExceptions) =>
        {
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransaction);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, exception));
        }, result =>
        {
            if (queryExecutionStarted)
                plan.NotifyExecutionFinished();
            ExecuteQueryPlanAfter(result);
        }, () => executionLease?.Dispose());
    }

    /// <summary>
    /// 使用查询计划执行异步查询，并复用根查询的连接、事务、参数和诊断生命周期。
    /// </summary>
    /// <typeparam name="TResult">执行结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="operation">调用 Dapper 查询 API 的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="acquireExecutionLease">是否为当前内部计划单独获取执行租约。</param>
    /// <param name="completeTransaction">当前内部计划成功后是否完成事务。</param>
    /// <returns>表示最终执行结果的异步操作。</returns>
    private async Task<TResult> InternalQueryPlanAsync<TResult>(SqlQueryPlan plan,
        Func<IDbConnection, string, object, IDbTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken, bool acquireExecutionLease = true, bool completeTransaction = true)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        using var operationCancellation = QueryResources.CreateOperationCancellationLease(cancellationToken);
        var operationToken = operationCancellation.Token;
        EnsureCancellationSupported(operationToken);
        if (operationToken.IsCancellationRequested)
        {
            QueryResources.MarkPreCancelledUnitOfWorkExecution();
            operationToken.ThrowIfCancellationRequested();
        }
        var executionLease = acquireExecutionLease ? QueryResources.AcquireExecutionLease() : null;
        TResult result = default;
        DiagnosticsMessage message = default;
        var queryExecutionStarted = false;
        return await SqlQueryPlanLifecycle.ExecuteAsync(async () =>
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (ExecuteBefore())
            {
                var preparedCommand = PrepareQueryPlan(plan);
                var connection = QueryResources.GetExecutionConnection();
                var transaction = await QueryResources.GetQueryTransactionAsync(operationToken).ConfigureAwait(false);
                message = CreateExecutionDiagnostics(preparedCommand, connection, plan);
                using var logScope = BeginExecutionLogScope(message);
                WritePlanTraceLog(preparedCommand);
                result = await operation(connection, preparedCommand.Sql, preparedCommand.DapperParameters, transaction,
                    operationToken);
                plan.NotifyExecutionCompleted();
                if (completeTransaction)
                    await QueryResourceCleanup.CompleteQueryTransactionAsync(operationToken).ConfigureAwait(false);
                ExecuteAfter(message);
            }
            return result;
        }, async (exception, cleanupExceptions) =>
        {
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, QueryResourceCleanup.RollbackQueryTransactionAsync)
                .ConfigureAwait(false);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, exception));
        }, result =>
        {
            if (queryExecutionStarted)
                plan.NotifyExecutionFinished();
            ExecuteQueryPlanAfter(result);
        }, () => executionLease?.Dispose()).ConfigureAwait(false);
    }

}
