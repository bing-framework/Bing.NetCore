using System.ComponentModel;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Metadata;
using Bing.Data;
using System.Text;

namespace Bing.Data.Sql;

/// <summary>
/// SQL Builder 的受控运行时协作入口。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlBuilderRuntimeBridge
{
    private static readonly string[] ParameterPrefixes = { "@", ":", "?", "$" };

    /// <summary>
    /// 判断 SQL 代码上下文是否包含指定参数标记。
    /// </summary>
    /// <param name="sql">待扫描的 SQL 文本。</param>
    /// <param name="parameterName">包含方言前缀的参数名称。</param>
    /// <returns>代码上下文包含该参数标记时返回 <see langword="true"/>。</returns>
    public static bool ContainsParameterToken(string sql, string parameterName) =>
        SqlBuilderBase.ContainsParameterToken(sql, parameterName);

    /// <summary>
    /// 判断查询是否需要执行快照。
    /// </summary>
    /// <param name="builder">待检查的 SQL Builder。</param>
    /// <returns>动态过滤或数据边界要求快照时返回 <see langword="true"/>。</returns>
    internal static bool RequiresExecutionSnapshot(ISqlBuilder builder) =>
        builder is SqlBuilderBase { RequiresRenderSnapshot: true };

    /// <summary>
    /// 判断查询派生是否需要保留隔离的规划 Builder。
    /// </summary>
    /// <param name="builder">已冻结的查询 Builder。</param>
    /// <returns>
    /// 包含 CTE、Union、分组、Distinct 或聚合结构时返回 <see langword="true"/>。
    /// </returns>
    /// <remarks>
    /// 此判断用于决定是否捕获完整渲染结构；创建计划时，已能从冻结结构完成
    /// Count 和任意排序分页的查询会释放规划来源，其余查询继续保留隔离副本。
    /// </remarks>
    internal static bool RequiresPlanningBuilder(ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (builder is ICteAccessor { CteItems.Count: > 0 } ||
            builder is IUnionAccessor { IsUnion: true })
            return true;
        if (builder is not ISqlQueryClauseAccessor accessor)
            return true;
        if (accessor.GroupByClause?.IsGroup == true)
            return true;
        return accessor.SelectClause is SelectClause selectClause &&
               (selectClause.IsDistinct || selectClause.HasAggregate);
    }

    /// <summary>
    /// 创建执行快照。
    /// </summary>
    /// <param name="builder">待冻结的 SQL Builder。</param>
    /// <returns>本次执行使用的 SQL 与 Builder 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(ISqlBuilder builder)
        => CreateExecutionSnapshot(builder, false);

    /// <summary>
    /// 创建执行快照。
    /// </summary>
    /// <param name="builder">待冻结的 SQL Builder。</param>
    /// <param name="includeDebugSql">是否同时生成调试 SQL。</param>
    /// <returns>本次执行使用的 SQL、参数与可选调试 SQL 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(ISqlBuilder builder, bool includeDebugSql)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (builder is SqlBuilderBase sqlBuilder)
        {
            var snapshot = sqlBuilder.CreateRenderSnapshot();
            return CreateExecutionSnapshot(snapshot, includeDebugSql);
        }
        var clone = builder.Clone();
        var sql = clone.ToSql();
        return new SqlBuilderExecutionSnapshot(sql, GetParameterSnapshot(clone),
            includeDebugSql ? clone.ToDebugSql(sql) : null,
            clone.Provider.ParamLiteralsResolver);
    }

    /// <summary>同时接收首次渲染的 SQL、参数来源和不可变 Select 边界。</summary>
    internal static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(SqlBuilderRenderSnapshot snapshot,
        bool includeDebugSql = false)
    {
        var parameters = snapshot.Parameters == null
            ? GetParameterSnapshot(snapshot.Builder) : GetParameterSnapshot(snapshot.Parameters);
        var resolver = snapshot.Builder.Provider.ParamLiteralsResolver;
        var debugSql = !includeDebugSql ? null : snapshot.Parameters == null
            ? snapshot.Builder.ToDebugSql(snapshot.Sql)
            : SqlBuilderBase.CreateDebugSql(snapshot.Sql, parameters, resolver);
        return new SqlBuilderExecutionSnapshot(snapshot.Sql, parameters, debugSql,
            resolver, snapshot.SelectShape, CreatePagination(snapshot.Builder, snapshot.SelectShape, snapshot.Parameters));
    }

    /// <summary>
    /// 使用调用方已经生成的 SQL 文本创建执行快照，避免重复渲染 Builder。
    /// </summary>
    /// <param name="builder">提供参数和执行上下文的 Builder。</param>
    /// <param name="sql">已经生成并与 Builder 形状对应的 SQL 文本。</param>
    /// <returns>包含给定 SQL 和参数快照的执行快照。</returns>
    internal static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(ISqlBuilder builder, string sql,
        SqlQuerySelectShape selectShape = null)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 文本不能为空。", nameof(sql));
        return new SqlBuilderExecutionSnapshot(sql, GetParameterSnapshot(builder), null,
            builder.Provider.ParamLiteralsResolver, selectShape, CreatePagination(builder, selectShape));
    }

    /// <summary>只开放框架容器的值转换，外部参数管理器继续使用原派生契约。</summary>
    private static SqlQueryPagination CreatePagination(ISqlBuilder builder, SqlQuerySelectShape selectShape,
        IParameterManager renderedParameters = null)
    {
        if (selectShape == null || builder is not ISqlCommonPartAccessor accessor)
            return null;
        var manager = renderedParameters ?? accessor.ParameterManager;
        var maximum = int.MaxValue;
        string provider = null;
        if (manager is ParameterLimitManagerBase limited)
            manager = limited.GetStorage(out maximum, out provider);
        if (manager.GetType() != typeof(ParameterManager))
            return null;
        var storage = (ParameterManager)manager;
        if (selectShape.HasPage)
            return new SqlQueryPagination(storage.ParameterDialect, builder.Provider.Dialect, null, selectShape.OffsetParameter,
                selectShape.LimitParameter, maximum, provider);
        if (!string.IsNullOrWhiteSpace(selectShape.LimitParameter))
            return null;
        if (builder is not SqlBuilderBase { FrozenPaginationRenderer: { } renderer })
            return null;
        storage.PreviewPageNames(out var offset, out var limit, selectShape.OffsetParameter);
        return new SqlQueryPagination(storage.ParameterDialect, builder.Provider.Dialect, renderer, offset, limit, maximum, provider);
    }

    /// <summary>
    /// 创建执行快照。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <returns>查询计划对应的 SQL 与参数快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(SqlQueryPlan plan)
        => CreateExecutionSnapshot(plan, false);

    /// <summary>判断查询计划是否已经包含冻结的执行快照。</summary>
    /// <param name="plan">待检查的查询计划。</param>
    /// <returns>包含冻结快照时返回 true；原生文本计划返回 false。</returns>
    public static bool HasExecutionSnapshot(SqlQueryPlan plan) => plan?.ExecutionSnapshot != null;

    /// <summary>
    /// 创建执行快照。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="includeDebugSql">是否同时生成调试 SQL。</param>
    /// <returns>查询计划对应的 SQL、参数与可选调试 SQL 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(SqlQueryPlan plan, bool includeDebugSql)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.QueryIr != null)
        {
            var irSnapshot = plan.QueryIr.Snapshot;
            if (includeDebugSql == false || irSnapshot.HasDebugSql)
                return irSnapshot;
            return irSnapshot.WithDebugSql();
        }
        var parameterSource = plan.Parameters;
        if (parameterSource == null || parameterSource is IEnumerable<SqlParam>)
        {
            var parameters = parameterSource as IEnumerable<SqlParam> ?? Array.Empty<SqlParam>();
            var debugSql = includeDebugSql
                ? SqlBuilderBase.CreateDebugSql(plan.CommandText, parameters, ParamLiteralsResolver.Instance)
                : null;
            return new SqlBuilderExecutionSnapshot(plan.CommandText, parameters, debugSql);
        }
        if (parameterSource is ISqlParameterMap parameterMap)
        {
            var mapParameters = GetParameterSnapshot(parameterMap, plan.CommandText);
            var debugSql = includeDebugSql
                ? SqlBuilderBase.CreateDebugSql(plan.CommandText, mapParameters, ParamLiteralsResolver.Instance, ignoreCase: true)
                : null;
            return new SqlBuilderExecutionSnapshot(plan.CommandText, mapParameters, debugSql);
        }
        throw new NotSupportedException("原生参数源尚未绑定，不能创建冻结执行快照；请先提供已解析的参数集合或映射。");
    }

    /// <summary>
    /// 在冻结之前校验 Builder 只包含查询操作。
    /// </summary>
    /// <param name="operation">Builder 当前的操作类型。</param>
    internal static void ValidateQueryOperation(SqlOperationKind operation)
    {
        if (operation is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            return;
        throw new InvalidOperationException("查询计划不能包含写入操作；请使用专用写入 Builder 和 ISqlExecutor.ExecuteReturning。");
    }

    /// <summary>
    /// 获取结构化查询计划使用的分页参数。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="pager">调用方指定的分页参数。</param>
    /// <returns>用于本次分页执行的参数。</returns>
    public static IPager GetPlanPager(SqlQueryPlan plan, IPager pager)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        return pager ?? plan.QueryIr?.CreateDefaultPagerSnapshot() ??
            throw new InvalidOperationException("分页参数不能为空。");
    }

    /// <summary>
    /// 创建总行数查询计划。
    /// </summary>
    /// <param name="plan">原始结构化查询计划。</param>
    /// <returns>返回单个总行数的查询计划。</returns>
    public static SqlQueryPlan CreateCountPlan(SqlQueryPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.QueryIr?.CanDeriveStructuredCount == true)
        {
            var countPlan = SqlQueryPlan.Create(plan.QueryIr.DeriveCount(), plan.SplitOn);
            countPlan.CopyContextFrom(plan, "Count");
            return countPlan;
        }
        // 规划源已经为本次派生创建独立副本，直接消费它，不再包装为计划后重复克隆。
        var planningBuilder = plan.CreatePlanningBuilder();
        if (planningBuilder != null)
        {
            var countPlan = CreateBuilderCountPlan(planningBuilder, plan.SplitOn);
            countPlan.CopyContextFrom(plan, "Count");
            return countPlan;
        }
        if (plan.QueryIr != null)
        {
            var countPlan = SqlQueryPlan.Create(plan.QueryIr.DeriveCount(), plan.SplitOn);
            countPlan.CopyContextFrom(plan, "Count");
            return countPlan;
        }
        var rawCountPlan = SqlQueryPlan.Create($"Select Count(*) From ({plan.CommandText}) As __bing_raw_count",
            plan.Parameters, plan.SplitOn, plan.CommandType);
        rawCountPlan.CopyContextFrom(plan, "Count");
        return rawCountPlan;
    }

    /// <summary>在一次派生独占的 Builder 上生成计数，结果只保留冻结 SQL 与参数。</summary>
    private static SqlQueryPlan CreateBuilderCountPlan(ISqlBuilder builder, string splitOn)
    {
        builder.ClearOrderBy();
        builder.ClearPageParams();
        var hasCte = builder is ICteAccessor { CteItems.Count: > 0 };
        var hasUnion = builder is IUnionAccessor { IsUnion: true };
        var hasGroup = builder is ISqlQueryClauseAccessor { GroupByClause.IsGroup: true };
        var hasDistinct = builder is ISqlQueryClauseAccessor { SelectClause.IsDistinct: true };
        var hasAggregate = builder is ISqlQueryClauseAccessor { SelectClause: SelectClause selectClause } &&
                           selectClause.HasAggregate;
        if ((hasCte || hasUnion) && builder is SqlBuilderBase)
        {
            var source = CreateExecutionSnapshot(builder);
            if (source.SelectShape != null)
                return SqlQueryPlan.Create(SqlQueryIr.Create(source, System.Data.CommandType.Text, builder.Pager)
                    .DeriveCount(), splitOn);
        }
        if (hasCte && (hasUnion || hasGroup || hasDistinct || hasAggregate))
            throw new NotSupportedException("包含 CTE 的 Union、Group 或 Distinct 查询暂不支持自动分页计数，请预先设置 TotalCount。");
        if (hasUnion || hasGroup || hasDistinct || hasAggregate)
        {
            SqlQueryPlan countPlan;
            if (builder is SqlBuilderBase)
            {
                // 内层 SQL 与参数来自同一渲染快照；外层只负责方言包装，不再合并或转换参数。
                var source = CreateExecutionSnapshot(builder);
                var countBuilder = builder.New().CountAll().From(new SqlQueryFragment(source.Sql), "t");
                var snapshot = source.WithSql(countBuilder.ToSql());
                countPlan = SqlQueryPlan.Create(SqlQueryIr.Create(snapshot, System.Data.CommandType.Text, builder.Pager),
                    splitOn);
            }
            else
            {
                var countBuilder = builder.New().CountAll().From(builder, "t");
                countPlan = CreateFrozenBuilderPlan(countBuilder, splitOn);
            }
            return countPlan;
        }
        builder.ClearSelect();
        return CreateFrozenBuilderPlan(builder.CountAll(), splitOn);
    }

    /// <summary>
    /// 创建分页查询计划。
    /// </summary>
    /// <param name="plan">原始结构化计划，或保留结构规划源的冻结计划。</param>
    /// <param name="pager">本次分页参数。</param>
    /// <returns>应用排序和分页后的数据查询计划。</returns>
    public static SqlQueryPlan CreatePagePlan(SqlQueryPlan plan, IPager pager)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        return TryCreateStructuredPagePlan(plan, pager) ??
            throw new ArgumentException("原生 SQL 分页必须通过 Provider Builder 创建分页计划。", nameof(plan));
    }

    /// <summary>
    /// 为执行器创建分页计划，仅在原生 SQL 包装时请求独立 Provider Builder。
    /// </summary>
    /// <param name="plan">原生 SQL、结构化或冻结查询计划。</param>
    /// <param name="pager">本次分页参数。</param>
    /// <param name="builderSource">绑定执行器当前数据源与 Provider 的 Builder 来源。</param>
    /// <returns>用于本次数据页执行的独立计划。</returns>
    public static SqlQueryPlan CreatePageExecutionPlan(SqlQueryPlan plan, IPager pager,
        ISqlQueryBuilderSource builderSource)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        if (builderSource == null)
            throw new ArgumentNullException(nameof(builderSource));
        return TryCreateStructuredPagePlan(plan, pager) ??
            CreatePagePlan(plan, pager, builderSource.CreateIndependentSqlBuilder());
    }

    /// <summary>先消费冻结 IR 或计划自己的规划源，不为结构化派生创建空 Builder。</summary>
    private static SqlQueryPlan TryCreateStructuredPagePlan(SqlQueryPlan plan, IPager pager)
    {
        if (plan.QueryIr?.CanDeriveStructuredPage(pager) == true)
            return CreateIrPagePlan(plan, pager);
        var builder = plan.CreatePlanningBuilder();
        return builder == null ? null : CreateBuilderPagePlan(plan, builder, pager);
    }

    /// <summary>沿用冻结 SQL 和执行上下文，仅更新两项分页参数。</summary>
    private static SqlQueryPlan CreateIrPagePlan(SqlQueryPlan plan, IPager pager)
    {
        var page = SqlQueryPlan.Create(plan.QueryIr.DerivePage(pager), plan.SplitOn);
        page.CopyContextFrom(plan, "Data");
        return page;
    }

    /// <summary>消费一次派生独占的 Builder，避免为已隔离的规划源再次复制查询树。</summary>
    private static SqlQueryPlan CreateBuilderPagePlan(SqlQueryPlan plan, ISqlBuilder builder, IPager pager)
    {
        builder.OrderBy(pager.Order);
        var pageBuilder = builder.Page(pager);
        var pagePlan = CreateFrozenBuilderPlan(pageBuilder, plan.SplitOn, forPaging: true);
        pagePlan.CopyContextFrom(plan, "Data");
        return pagePlan;
    }

    /// <summary>
    /// 冻结一次派生结果，分页扩展按需保留同次渲染的隔离规划来源。
    /// </summary>
    /// <param name="builder">用于生成 SQL 的独立 Builder。</param>
    /// <param name="splitOn">Dapper 多映射使用的分段列名称。</param>
    /// <param name="forPaging">是否允许后续继续按原查询结构派生分页和计数。</param>
    /// <returns>执行时只消费冻结 SQL 和参数的计划。</returns>
    private static SqlQueryPlan CreateFrozenBuilderPlan(ISqlBuilder builder, string splitOn, bool forPaging = false)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        SqlBuilderExecutionSnapshot snapshot;
        if (builder is SqlBuilderBase sqlBuilder)
        {
            var rendered = sqlBuilder.CreateRenderSnapshot();
            snapshot = CreateExecutionSnapshot(rendered);
            builder = rendered.Builder;
        }
        else
        {
            // 沿用非基类 Builder 的快照克隆契约，并保留实际渲染的来源。
            builder = builder.Clone();
            var sql = builder.ToSql();
            snapshot = CreateExecutionSnapshot(builder, sql);
        }
        var queryIr = SqlQueryIr.Create(snapshot, System.Data.CommandType.Text, builder.Pager);
        return SqlQueryPlan.Create(queryIr, splitOn, forPaging ? builder : null);
    }

    /// <summary>
    /// 创建分页查询计划。
    /// </summary>
    /// <param name="plan">原生 SQL 文本计划、结构化计划或其冻结计划。</param>
    /// <param name="pager">本次分页参数。</param>
    /// <param name="pageBuilder">绑定当前 Provider 的独立 Builder。</param>
    /// <returns>应用安全排序和 Provider 分页语法的分页计划。</returns>
    public static SqlQueryPlan CreatePagePlan(SqlQueryPlan plan, IPager pager, ISqlBuilder pageBuilder)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        if (pageBuilder == null)
            throw new ArgumentNullException(nameof(pageBuilder));
        var structuredPlan = TryCreateStructuredPagePlan(plan, pager);
        if (structuredPlan != null)
            return structuredPlan;
        var provider = pageBuilder.Provider;
        var profile = SqlProviderCapabilityResolver.GetProfile(provider);
        var reason = SqlProviderCapabilityResolver.HasProfile(provider) == false
            ? SqlCapabilityFailureReason.ProviderProfileMissing
            : SqlProviderCapabilityResolver.HasCompleteProfile(provider) == false
                ? SqlCapabilityFailureReason.ProviderProfileMismatch
                : SqlCapabilityFailureReason.ProviderImplementationGap;
        if (profile.Query?.Pagination != SqlQueryCapabilityState.Supported)
            throw SqlCapabilityFailure.Create(reason, "Query:RawPagination", provider.Key,
                "当前 SQL Provider 不支持原生 SQL 自动分页。");

        var order = ValidateRawPageOrder(pager.Order);
        var parameterManager = (pageBuilder as ISqlCommonPartAccessor)?.ParameterManager ??
            throw new NotSupportedException("当前 SQL Provider 不支持原生 SQL 分页参数绑定。");
        var offsetParameter = NextRawPageParameter(parameterManager, plan.CommandText, "offset");
        var limitParameter = NextRawPageParameter(parameterManager, plan.CommandText, "limit");
        pageBuilder.AppendFrom($"({plan.CommandText}) __bing_raw_page");
        pageBuilder.Select("*");
        pageBuilder.OrderBy(order);
        var sql = $"{pageBuilder.ToSql()} {pageBuilder.Provider.PaginationRenderer.Render(offsetParameter, limitParameter)}";
        var parameters = new SqlRawPagingParameterMap(plan.Parameters, offsetParameter, pager.GetSkipCount(),
            limitParameter, pager.PageSize);
        var pagePlan = SqlQueryPlan.Create(sql, parameters, plan.SplitOn, plan.CommandType);
        pagePlan.CopyContextFrom(plan, "Data");
        return pagePlan;
    }

    /// <summary>
    /// 获取 Builder 当前参数的独立快照。
    /// </summary>
    /// <param name="builder">待读取参数的 SQL Builder。</param>
    /// <returns>当前 Builder 的参数快照集合。</returns>
    private static IReadOnlyCollection<SqlParam> GetParameterSnapshot(ISqlBuilder builder)
    {
        if (builder is not ISqlCommonPartAccessor accessor)
            return builder.GetParams().Select(item => new SqlParam(item.Key, item.Value)).ToArray();
        return GetParameterSnapshot(accessor.ParameterManager);
    }

    /// <summary>冻结实际参与同次渲染的参数，保留集合值及元数据的既有快照合同。</summary>
    private static IReadOnlyCollection<SqlParam> GetParameterSnapshot(IParameterManager parameterManager)
    {
        if (parameterManager is IAdvancedParameterManager advanced)
            return advanced.GetSqlParams().Values.Where(parameter => parameter != null)
                .Select(parameter => SqlParameterSnapshot.CloneSqlParameter(parameter,
                    parameterManager.NormalizeName(parameter.Name))).ToArray();
        return parameterManager.GetParams()
            .Select(item => new SqlParam(parameterManager.NormalizeName(item.Key), item.Value)
            {
                Source = SqlParameterSource.Basic,
                MetadataLevel = SqlParameterMetadataLevel.Weak
            })
            .ToArray();
    }

    /// <summary>
    /// 获取已经解析的原生参数映射快照。
    /// </summary>
    /// <param name="map">原生参数映射。</param>
    /// <param name="sql">当前 SQL 文本。</param>
    /// <returns>可供执行快照使用的参数集合。</returns>
    /// <remarks>
    /// 这里只消费映射项已经解析出的值，不在 RuntimeBridge 中重新引入 POCO 反射绑定。
    /// </remarks>
    private static IReadOnlyCollection<SqlParam> GetParameterSnapshot(ISqlParameterMap map, string sql)
    {
        var items = map.GetItems() ?? Array.Empty<SqlParameterMapItem>();
        var parameters = new List<SqlParam>(items.Count);
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
                continue;
            if (item.ValueResolved == false && item.Direction is not (System.Data.ParameterDirection.Output or
                System.Data.ParameterDirection.ReturnValue))
                throw new NotSupportedException($"原生参数映射 '{item.Name}' 尚未解析输入值，不能创建冻结执行快照。");
            var name = NormalizeMapParameterName(sql, item.Name);
            parameters.Add(new SqlParam(name, item.Value, item.DbType, item.Direction, item.Size,
                item.Precision, item.Scale)
            {
                EntityType = item.EntityType,
                PropertyName = item.PropertyName,
                Source = SqlParameterSource.RawSql,
                MetadataLevel = SqlParameterMetadataLevel.Weak
            });
        }
        return parameters;
    }

    /// <summary>
    /// 根据 SQL 中已有的占位符补全参数前缀。
    /// </summary>
    /// <param name="sql">当前 SQL 文本。</param>
    /// <param name="name">映射项参数名。</param>
    /// <returns>与 SQL 占位符一致的参数名。</returns>
    private static string NormalizeMapParameterName(string sql, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name[0] is '@' or ':' or '?' or '$')
            return name;
        foreach (var prefix in ParameterPrefixes)
            if (SqlBuilderBase.ContainsParameterToken(sql, prefix + name, ignoreCase: true))
                return prefix + name;
        return name;
    }

    /// <summary>
    /// 验证并规范化原生 SQL 分页排序表达式。
    /// </summary>
    /// <param name="order">逗号分隔的排序列及可选 <c>ASC</c>/<c>DESC</c> 方向。</param>
    /// <returns>格式化后的安全排序表达式。</returns>
    /// <exception cref="ArgumentException">排序为空、包含非法标识符或使用不支持的排序方向时抛出。</exception>
    /// <remarks>仅接受字母、数字、下划线和点号组成的标识符，避免未参数化排序片段进入分页 SQL。</remarks>
    private static string ValidateRawPageOrder(string order)
    {
        if (string.IsNullOrWhiteSpace(order))
            throw new ArgumentException("原生 SQL 分页必须提供排序列。", nameof(order));
        var result = new List<string>();
        foreach (var item in order.Split(','))
        {
            var value = item.Trim();
            var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length is < 1 or > 2 || (parts.Length == 2 &&
                string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase) == false &&
                string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase) == false) ||
                IsSafeIdentifier(parts[0]) == false)
                throw new ArgumentException("原生 SQL 分页排序只能包含字母、数字、下划线、点号和 ASC/DESC。", nameof(order));
            result.Add(string.Join(" ", parts));
        }
        return string.Join(", ", result);
    }

    /// <summary>
    /// 判断文本是否仅由安全的点分隔标识符组成。
    /// </summary>
    /// <param name="value">待检查的标识符文本。</param>
    /// <returns>文本符合安全标识符格式时返回 <see langword="true"/>。</returns>
    private static bool IsSafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var segments = value.Split('.');
        return segments.All(segment => segment.Length > 0 &&
            (char.IsLetter(segment[0]) || segment[0] == '_') &&
            segment.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_'));
    }

    /// <summary>
    /// 生成不与原生 SQL 可执行上下文中参数标记冲突的分页参数名。
    /// </summary>
    /// <param name="parameterManager">生成候选参数名称的参数管理器。</param>
    /// <param name="sql">需要检查参数标记冲突的原生 SQL。</param>
    /// <param name="suffix">保留的分页参数命名后缀；当前实现不参与参数名生成。</param>
    /// <returns>未出现在原生 SQL 可执行上下文中的参数名称。</returns>
    private static string NextRawPageParameter(IParameterManager parameterManager, string sql, string suffix)
    {
        while (true)
        {
            var name = parameterManager.GenerateName();
            if (ContainsParameterToken(sql, name) == false)
                return name;
        }
    }

    /// <summary>
    /// 将结构化实体表追加为根来源。
    /// </summary>
    /// <param name="builder">要追加根来源的 SQL Builder。</param>
    /// <param name="reference">结构化实体表引用。</param>
    public static void AppendRoot(ISqlBuilder builder, SqlTableReference reference)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        ((ISqlQueryClauseAccessor)builder).FromClause.From(reference);
    }

    /// <summary>
    /// 将类型化派生表设置为根来源。
    /// </summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="builder">要设置根来源的 SQL Builder。</param>
    /// <param name="subquery">类型化派生表。</param>
    public static void FromSubquery<TProjection>(ISqlBuilder builder, SqlSubquery<TProjection> subquery)
        where TProjection : class
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        ((FromClause)((ISqlQueryClauseAccessor)builder).FromClause).From(subquery);
    }

    /// <summary>
    /// 为原生 SQL 分页合并来源参数与分页参数的映射对象。
    /// </summary>
    private sealed class SqlRawPagingParameterMap : ISqlParameterMap
    {
        /// <summary>
        /// 合并后的分页参数项集合。
        /// </summary>
        private readonly IReadOnlyCollection<SqlParameterMapItem> _items;

        /// <summary>
        /// 初始化一个 <see cref="SqlRawPagingParameterMap"/> 类型的实例。
        /// </summary>
        /// <param name="source">原生 SQL 的来源参数。</param>
        /// <param name="offsetName">分页偏移量参数名称。</param>
        /// <param name="offset">分页偏移量。</param>
        /// <param name="limitName">分页大小参数名称。</param>
        /// <param name="limit">分页大小。</param>
        public SqlRawPagingParameterMap(object source, string offsetName, int offset, string limitName, int limit)
        {
            if (source is ISqlParameterMap parameterMap)
            {
                Source = parameterMap.Source;
                _items = parameterMap.GetItems().Concat(CreateItems(offsetName, offset, limitName, limit)).ToArray();
            }
            else
            {
                Source = source;
                _items = CreateItems(offsetName, offset, limitName, limit).ToArray();
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 获取原生 SQL 的来源参数对象。
        /// </remarks>
        public object Source { get; }

        /// <inheritdoc />
        public IReadOnlyCollection<SqlParameterMapItem> GetItems() => _items;

        /// <summary>
        /// 创建偏移量和分页大小参数项。
        /// </summary>
        /// <param name="offsetName">分页偏移量参数名称。</param>
        /// <param name="offset">分页偏移量。</param>
        /// <param name="limitName">分页大小参数名称。</param>
        /// <param name="limit">分页大小。</param>
        /// <returns>创建的分页参数项集合。</returns>
        private static IEnumerable<SqlParameterMapItem> CreateItems(string offsetName, int offset, string limitName,
            int limit)
        {
            yield return CreateItem(offsetName, offset);
            yield return CreateItem(limitName, limit);
        }

        /// <summary>
        /// 创建一个带有显式值的 SQL 参数项。
        /// </summary>
        /// <param name="name">参数名称。</param>
        /// <param name="value">参数值。</param>
        /// <returns>创建的 SQL 参数项。</returns>
        private static SqlParameterMapItem CreateItem(string name, int value) => new()
        {
            Name = name,
            Value = value,
            HasExplicitValue = true,
            ValueResolved = true
        };
    }
}
