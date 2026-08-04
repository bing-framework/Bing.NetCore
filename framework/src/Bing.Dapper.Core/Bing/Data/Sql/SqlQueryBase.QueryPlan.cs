using Bing.Helpers;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划执行
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
    /// <returns>最终执行结果。</returns>
    private TResult InternalQueryPlan<TResult>(SqlQueryPlan plan,
        Func<IDbConnection, string, object, IDbTransaction, TResult> operation)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        using var debugLogScope = BeginQueryPlanDebugLogScope();
        ValidateQueryBuilder(plan.Builder);
        var executionLease = AcquireExecutionLease();
        TResult result = default;
        DiagnosticsMessage message = default;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (ExecuteBefore())
            {
                var preparedPlan = PrepareQueryPlan(plan);
                var connection = GetExecutionConnection();
                var transaction = GetQueryTransaction();
                message = ExecuteBefore(preparedPlan.Sql, preparedPlan.ParameterSource, connection,
                    preparedPlan.ParameterDiagnostics);
                WritePlanTraceLog(preparedPlan);
                result = operation(connection, preparedPlan.Sql, preparedPlan.DapperParameters, transaction);
                CompleteQueryTransaction();
                ExecuteAfter(message);
            }
        }
        catch (Exception exception)
        {
            primaryException = exception;
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, exception));
        }
        finally
        {
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteQueryPlanAfter(result));
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <summary>
    /// 使用查询计划执行异步查询，并复用根查询的连接、事务、参数和诊断生命周期。
    /// </summary>
    /// <typeparam name="TResult">执行结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="operation">调用 Dapper 查询 API 的操作。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终执行结果的异步操作。</returns>
    private async Task<TResult> InternalQueryPlanAsync<TResult>(SqlQueryPlan plan,
        Func<IDbConnection, string, object, IDbTransaction, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        using var debugLogScope = BeginQueryPlanDebugLogScope();
        cancellationToken.ThrowIfCancellationRequested();
        ValidateQueryBuilder(plan.Builder);
        var executionLease = AcquireExecutionLease();
        TResult result = default;
        DiagnosticsMessage message = default;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (ExecuteBefore())
            {
                var preparedPlan = PrepareQueryPlan(plan);
                var connection = GetExecutionConnection();
                var transaction = GetQueryTransaction();
                message = ExecuteBefore(preparedPlan.Sql, preparedPlan.ParameterSource, connection,
                    preparedPlan.ParameterDiagnostics);
                WritePlanTraceLog(preparedPlan);
                result = await operation(connection, preparedPlan.Sql, preparedPlan.DapperParameters, transaction);
                CompleteQueryTransaction();
                ExecuteAfter(message);
            }
        }
        catch (Exception exception)
        {
            primaryException = exception;
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteError(message, exception));
        }
        finally
        {
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, () => ExecuteQueryPlanAfter(result));
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

}