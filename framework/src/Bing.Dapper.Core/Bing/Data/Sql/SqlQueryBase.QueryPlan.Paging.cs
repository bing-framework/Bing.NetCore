using Bing.Helpers;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划分页执行
public abstract partial class SqlQueryBase
{
    /// <inheritdoc />
    PagerList<TResult> ISqlQueryPlanExecutor.ToPage<TResult>(SqlQueryPlan plan, IPager pager, int? timeout)
    {
        var sourcePager = SqlBuilderRuntimeBridge.GetPlanPager(plan, pager);
        var page = CreatePlanPagerSnapshot(sourcePager);
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var queryExecutionStarted = false;
        try
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            var totalCount = page.TotalCount;
            var totalCountCalculated = false;
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
                totalCount = InternalQueryPlan(countPlan, (connection, sql, parameters, transaction) =>
                    Conv.ToInt(connection.ExecuteScalar(sql, parameters, transaction, timeout,
                        commandType: countPlan.CommandType)), acquireExecutionLease: false, completeTransaction: false);
                totalCountCalculated = true;
            }
            var pagePlan = plan.IsBuilderPlan
                ? SqlBuilderRuntimeBridge.CreatePagePlan(plan, page)
                : SqlBuilderRuntimeBridge.CreatePagePlan(plan, page, CreateIndependentSqlBuilder());
            var items = InternalQueryPlan(pagePlan, (connection, sql, parameters, transaction) => connection
                .Query<TResult>(sql, parameters, transaction, buffered: true, commandTimeout: timeout,
                    commandType: pagePlan.CommandType).ToList(), acquireExecutionLease: false,
                completeTransaction: false);
            CompleteQueryTransaction();
            if (totalCountCalculated)
            {
                page.TotalCount = totalCount;
                sourcePager.TotalCount = totalCount;
            }
            result = new PagerList<TResult>(page, items);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
        }
        finally
        {
            if (queryExecutionStarted)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, plan.NotifyExecutionFinished);
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <inheritdoc />
    async Task<PagerList<TResult>> ISqlQueryPlanExecutor.ToPageAsync<TResult>(SqlQueryPlan plan, IPager pager,
        int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePager = SqlBuilderRuntimeBridge.GetPlanPager(plan, pager);
        var page = CreatePlanPagerSnapshot(sourcePager);
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        var queryExecutionStarted = false;
        try
        {
            plan.NotifyExecutionStarted();
            queryExecutionStarted = true;
            var totalCount = page.TotalCount;
            var totalCountCalculated = false;
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(plan);
                totalCount = await InternalQueryPlanAsync(countPlan, async (connection, sql, parameters, transaction) =>
                    Conv.ToInt(await connection.ExecuteScalarAsync(CreateQueryCommandDefinition(sql, parameters,
                        transaction, timeout, buffered: true, cancellationToken, countPlan.CommandType))), cancellationToken,
                    acquireExecutionLease: false, completeTransaction: false);
                totalCountCalculated = true;
            }
            var pagePlan = plan.IsBuilderPlan
                ? SqlBuilderRuntimeBridge.CreatePagePlan(plan, page)
                : SqlBuilderRuntimeBridge.CreatePagePlan(plan, page, CreateIndependentSqlBuilder());
            var items = await InternalQueryPlanAsync(pagePlan, async (connection, sql, parameters, transaction) =>
                await ExecuteMaterializedQueryAsync<TResult>(connection,
                    CreateQueryCommandDefinition(sql, parameters, transaction, timeout, buffered: true, cancellationToken,
                        pagePlan.CommandType), cancellationToken), cancellationToken, acquireExecutionLease: false,
                completeTransaction: false);
            await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            if (totalCountCalculated)
            {
                page.TotalCount = totalCount;
                sourcePager.TotalCount = totalCount;
            }
            result = new PagerList<TResult>(page, items);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, RollbackQueryTransactionAsync)
                .ConfigureAwait(false);
        }
        finally
        {
            if (queryExecutionStarted)
                SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, plan.NotifyExecutionFinished);
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <summary>
    /// 判断分页总数是否需要由当前查询计划自动计算。
    /// </summary>
    /// <remarks>
    /// 兼容未采用 <see cref="Pager.IsTotalCountKnown"/> 的第三方 <see cref="IPager"/>：其非零总数沿用既有语义视为已知。
    /// </remarks>
    private static bool IsPlanTotalCountUnknown(IPager pager) => pager is Pager knownPager
        ? knownPager.IsTotalCountKnown == false
        : pager.TotalCount == 0;

    /// <summary>
    /// 创建本次分页执行使用的不可变输入快照。
    /// </summary>
    /// <param name="source">调用方或查询计划提供的分页参数。</param>
    /// <returns>与调用方对象隔离的分页参数副本。</returns>
    private static Pager CreatePlanPagerSnapshot(IPager source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return new Pager(source.Page, source.PageSize, source.TotalCount, source.Order,
            source is Pager pager && pager.IsTotalCountKnown);
    }

}