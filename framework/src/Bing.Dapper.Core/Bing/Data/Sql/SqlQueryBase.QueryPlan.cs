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
    /// 异步执行查询并完整物化结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行类型。</typeparam>
    /// <param name="connection">数据库连接。</param>
    /// <param name="command">Dapper 命令定义。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已物化的结果集合。</returns>
    /// <remarks>
    /// 命令已携带取消令牌；统一委托 Dapper 处理标量和对象投影，避免自定义 Reader 解析器在标量类型上产生无效转换。
    /// </remarks>
    protected virtual async Task<List<TResult>> ExecuteMaterializedQueryAsync<TResult>(IDbConnection connection,
        CommandDefinition command, CancellationToken cancellationToken) =>
        (await connection.QueryAsync<TResult>(command)).ToList();

    /// <summary>
    /// 创建异步查询使用的 Dapper 命令定义。
    /// </summary>
    /// <param name="sql">待执行的 SQL。</param>
    /// <param name="parameters">数据库参数。</param>
    /// <param name="transaction">当前事务。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="buffered">是否缓冲结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="commandType">命令类型。</param>
    /// <returns>Dapper 命令定义。</returns>
    protected virtual CommandDefinition CreateQueryCommandDefinition(string sql, object parameters,
        IDbTransaction transaction, int? timeout, bool buffered, CancellationToken cancellationToken = default,
        CommandType? commandType = null) => new(sql, parameters, transaction, timeout, commandType,
        buffered ? CommandFlags.Buffered : CommandFlags.None, cancellationToken);

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
        if (SqlBuilderRuntimeBridge.ValidateQueryPlan(plan))
            EnsureWritableDataSource();
        var executionLease = acquireExecutionLease ? AcquireExecutionLease() : null;
        TResult result = default;
        DiagnosticsMessage message = default;
        var queryExecutionStarted = false;
        return SqlQueryPlanLifecycle.Execute(() =>
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (ExecuteBefore())
            {
                var preparedPlan = PrepareQueryPlan(plan);
                var connection = GetExecutionConnection();
                var transaction = GetQueryTransaction();
                message = CreateExecutionDiagnostics(preparedPlan.Command, connection, plan);
                using var logScope = BeginExecutionLogScope(message);
                WritePlanTraceLog(preparedPlan);
                result = operation(connection, preparedPlan.Sql, preparedPlan.DapperParameters, transaction);
                plan.NotifyExecutionCompleted();
                if (completeTransaction)
                    CompleteQueryTransaction();
                ExecuteAfter(message);
            }
            return result;
        }, (exception, cleanupExceptions) =>
        {
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
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
        Func<IDbConnection, string, object, IDbTransaction, Task<TResult>> operation,
        CancellationToken cancellationToken, bool acquireExecutionLease = true, bool completeTransaction = true)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (SqlBuilderRuntimeBridge.ValidateQueryPlan(plan))
            EnsureWritableDataSource();
        var executionLease = acquireExecutionLease ? AcquireExecutionLease() : null;
        TResult result = default;
        DiagnosticsMessage message = default;
        var queryExecutionStarted = false;
        return await SqlQueryPlanLifecycle.ExecuteAsync(async () =>
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            if (ExecuteBefore())
            {
                var preparedPlan = PrepareQueryPlan(plan);
                var connection = GetExecutionConnection();
                var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
                message = CreateExecutionDiagnostics(preparedPlan.Command, connection, plan);
                using var logScope = BeginExecutionLogScope(message);
                WritePlanTraceLog(preparedPlan);
                result = await operation(connection, preparedPlan.Sql, preparedPlan.DapperParameters, transaction);
                plan.NotifyExecutionCompleted();
                if (completeTransaction)
                    await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
                ExecuteAfter(message);
            }
            return result;
        }, async (exception, cleanupExceptions) =>
        {
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, RollbackQueryTransactionAsync)
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