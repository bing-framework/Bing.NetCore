using Bing.Helpers;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

// Sql查询对象 - 独立查询计划分页执行
public abstract partial class SqlQueryBase
{
    /// <inheritdoc />
    PagerList<TResult> ISqlQueryPlanExecutor.ToPage<TResult>(SqlQueryPlan plan, IPager pager, int? timeout)
    {
        var sourcePager = GetPlanPager(plan, pager);
        var page = CreatePlanPagerSnapshot(sourcePager);
        var sourceBuilder = CreatePlanBuilderSnapshot(plan.Builder);
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            var totalCount = page.TotalCount;
            var totalCountCalculated = false;
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlQueryPlan.Create(CreatePlanCountBuilder(sourceBuilder));
                totalCount = InternalQueryPlan(countPlan, (connection, sql, parameters, transaction) =>
                    Conv.ToInt(connection.ExecuteScalar(sql, parameters, transaction, timeout,
                        commandType: countPlan.CommandType)), acquireExecutionLease: false, completeTransaction: false);
                totalCountCalculated = true;
            }
            var pagePlan = SqlQueryPlan.Create(CreatePlanPageBuilder(sourceBuilder, page), plan.SplitOn);
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
        SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, executionLease.Dispose);
        SqlQueryPlanLifecycle.ThrowExceptions(primaryException, cleanupExceptions);
        return result;
    }

    /// <inheritdoc />
    async Task<PagerList<TResult>> ISqlQueryPlanExecutor.ToPageAsync<TResult>(SqlQueryPlan plan, IPager pager,
        int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePager = GetPlanPager(plan, pager);
        var page = CreatePlanPagerSnapshot(sourcePager);
        var sourceBuilder = CreatePlanBuilderSnapshot(plan.Builder);
        var executionLease = AcquireExecutionLease();
        PagerList<TResult> result = null;
        Exception primaryException = null;
        var cleanupExceptions = new List<Exception>();
        try
        {
            var totalCount = page.TotalCount;
            var totalCountCalculated = false;
            if (IsPlanTotalCountUnknown(page))
            {
                var countPlan = SqlQueryPlan.Create(CreatePlanCountBuilder(sourceBuilder));
                totalCount = await InternalQueryPlanAsync(countPlan, async (connection, sql, parameters, transaction) =>
                    Conv.ToInt(await connection.ExecuteScalarAsync(CreateQueryCommandDefinition(sql, parameters,
                        transaction, timeout, buffered: true, cancellationToken, countPlan.CommandType))), cancellationToken,
                    acquireExecutionLease: false, completeTransaction: false);
                totalCountCalculated = true;
            }
            var pagePlan = SqlQueryPlan.Create(CreatePlanPageBuilder(sourceBuilder, page), plan.SplitOn);
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

    /// <summary>
    /// 创建当前分页操作使用的 Builder 输入快照。
    /// </summary>
    /// <param name="source">查询计划持有的源 Builder。</param>
    /// <returns>已应用动态过滤器的独立 Builder 副本。</returns>
    private static ISqlBuilder CreatePlanBuilderSnapshot(ISqlBuilder source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source is SqlBuilderBase { RequiresRenderSnapshot: true } builder)
            return builder.CreateRenderSnapshot().Builder.Clone();
        return source.Clone();
    }

    /// <summary>
    /// 创建当前页数据使用的独立 Builder。
    /// </summary>
    /// <param name="source">本次执行开始时冻结的源 Builder。</param>
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
    /// <param name="source">本次执行开始时冻结的源 Builder。</param>
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
        var hasAggregate = builder is ISqlQueryClauseAccessor { SelectClause: SelectClause selectClause } &&
                           selectClause.HasAggregate;
        if (hasCte && (hasUnion || hasGroup || hasDistinct || hasAggregate))
            throw new NotSupportedException("包含 CTE 的 Union、Group 或 Distinct 查询暂不支持自动分页计数，请预先设置 TotalCount。");
        if (hasUnion || hasGroup || hasDistinct || hasAggregate)
            return builder.New().CountAll().From(builder, "t");
        builder.ClearSelect();
        return builder.CountAll();
    }
}