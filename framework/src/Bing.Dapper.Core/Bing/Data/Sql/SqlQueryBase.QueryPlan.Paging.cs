using Bing.Helpers;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划分页执行
public abstract partial class SqlQueryBase
{
    /// <inheritdoc />
    PagerList<TResult> ISqlQueryPlanExecutor.ToPage<TResult>(SqlQueryPlan plan, IPager pager, int? timeout)
    {
        var page = GetPlanPager(plan, pager);
        using var debugLogScope = BeginQueryPlanDebugLogScope();
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlQueryPlan.Create(CreatePlanCountBuilder(plan.Builder));
                page.TotalCount = InternalQueryPlan(countPlan, (connection, sql, parameters, transaction) =>
                    Conv.ToInt(connection.ExecuteScalar(sql, parameters, transaction, timeout,
                        commandType: countPlan.CommandType)), acquireExecutionLease: false, completeTransaction: false,
                    consumeDebugLogState: false);
            }
            var pagePlan = SqlQueryPlan.Create(CreatePlanPageBuilder(plan.Builder, page), plan.SplitOn);
            var items = InternalQueryPlan(pagePlan, (connection, sql, parameters, transaction) => connection
                .Query<TResult>(sql, parameters, transaction, buffered: true, commandTimeout: timeout,
                    commandType: pagePlan.CommandType).ToList(), acquireExecutionLease: false,
                consumeDebugLogState: false);
            result = new PagerList<TResult>(page, items);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <inheritdoc />
    async Task<PagerList<TResult>> ISqlQueryPlanExecutor.ToPageAsync<TResult>(SqlQueryPlan plan, IPager pager,
        int? timeout, CancellationToken cancellationToken)
    {
        var page = GetPlanPager(plan, pager);
        using var debugLogScope = BeginQueryPlanDebugLogScope();
        cancellationToken.ThrowIfCancellationRequested();
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlQueryPlan.Create(CreatePlanCountBuilder(plan.Builder));
                page.TotalCount = await InternalQueryPlanAsync(countPlan, async (connection, sql, parameters, transaction) =>
                    Conv.ToInt(await connection.ExecuteScalarAsync(CreateQueryCommandDefinition(sql, parameters,
                        transaction, timeout, buffered: true, cancellationToken, countPlan.CommandType))), cancellationToken,
                    acquireExecutionLease: false, completeTransaction: false, consumeDebugLogState: false);
            }
            var pagePlan = SqlQueryPlan.Create(CreatePlanPageBuilder(plan.Builder, page), plan.SplitOn);
            var items = await InternalQueryPlanAsync(pagePlan, async (connection, sql, parameters, transaction) =>
                await ExecuteMaterializedQueryAsync<TResult>(connection,
                    CreateQueryCommandDefinition(sql, parameters, transaction, timeout, buffered: true, cancellationToken,
                        pagePlan.CommandType), cancellationToken), cancellationToken, acquireExecutionLease: false,
                consumeDebugLogState: false);
            result = new PagerList<TResult>(page, items);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <summary>
    /// 获取结构化查询计划使用的分页参数。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="pager">调用方指定的分页参数。</param>
    /// <returns>用于本次分页执行的参数。</returns>
    private static IPager GetPlanPager(SqlQueryPlan plan, IPager pager)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.IsBuilderPlan == false)
            throw new NotSupportedException("原生 SQL 文本查询不支持自动分页，请使用结构化 Fluent 查询描述。");
        return pager ?? plan.Builder.Pager ?? throw new InvalidOperationException("分页参数不能为空。");
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
    /// 创建当前页数据使用的独立 Builder。
    /// </summary>
    /// <param name="source">查询描述持有的源 Builder。</param>
    /// <param name="pager">分页参数。</param>
    /// <returns>已应用排序和分页的独立 Builder。</returns>
    private static ISqlBuilder CreatePlanPageBuilder(ISqlBuilder source, IPager pager)
    {
        var builder = source.Clone();
        builder.OrderBy(pager.Order);
        return builder.Page(pager);
    }

    /// <summary>
    /// 创建总行数查询使用的独立 Builder。
    /// </summary>
    /// <param name="source">查询描述持有的源 Builder。</param>
    /// <returns>返回单个总行数的 Builder。</returns>
    private static ISqlBuilder CreatePlanCountBuilder(ISqlBuilder source)
    {
        var builder = source.Clone();
        builder.ClearOrderBy();
        builder.ClearPageParams();
        var hasCte = builder is ICteAccessor { CteItems.Count: > 0 };
        var hasUnion = builder is IUnionAccessor { IsUnion: true };
        var hasGroup = builder is ISqlQueryClauseAccessor { GroupByClause.IsGroup: true };
        var hasDistinct = builder is ISqlQueryClauseAccessor { SelectClause.IsDistinct: true };
        if (hasCte && (hasUnion || hasGroup || hasDistinct))
            throw new NotSupportedException("包含 CTE 的 Union、Group 或 Distinct 查询暂不支持自动分页计数，请预先设置 TotalCount。");
        if (hasUnion || hasDistinct)
            return builder.New().Count().From(builder, "t");
        if (hasGroup)
        {
            builder.ClearSelect();
            return builder.New().Count().From(builder.AppendSelect("1 As c"), "t");
        }
        builder.ClearSelect();
        return builder.Count();
    }
}