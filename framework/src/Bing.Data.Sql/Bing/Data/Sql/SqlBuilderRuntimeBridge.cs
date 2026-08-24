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
    /// <summary>
    /// 判断 SQL 代码上下文是否包含指定参数标记。
    /// </summary>
    /// <param name="sql">待扫描的 SQL 文本。</param>
    /// <param name="parameterName">包含方言前缀的参数名称。</param>
    /// <returns>代码上下文包含该参数标记时返回 <see langword="true"/>。</returns>
    public static bool ContainsParameterToken(string sql, string parameterName) =>
        SqlBuilderBase.ContainsParameterToken(sql, parameterName);

    /// <summary>
    /// 判断 Builder 是否必须创建独立执行快照。
    /// </summary>
    /// <param name="builder">待检查的 SQL Builder。</param>
    /// <returns>动态过滤或数据边界要求快照时返回 <see langword="true"/>。</returns>
    internal static bool RequiresExecutionSnapshot(ISqlBuilder builder) =>
        builder is SqlBuilderBase { RequiresRenderSnapshot: true };

    /// <summary>
    /// 创建 SQL 与参数状态一致的执行快照。
    /// </summary>
    /// <param name="builder">待冻结的 SQL Builder。</param>
    /// <returns>本次执行使用的 SQL 与 Builder 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(ISqlBuilder builder)
        => CreateExecutionSnapshot(builder, false);

    /// <summary>
    /// 创建 SQL 与参数状态一致的执行快照，并按需生成调试 SQL。
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
            return new SqlBuilderExecutionSnapshot(snapshot.Sql, GetParameterSnapshot(snapshot.Builder),
                includeDebugSql ? snapshot.Builder.ToDebugSql(snapshot.Sql) : null);
        }
        var clone = builder.Clone();
        var sql = clone.ToSql();
        return new SqlBuilderExecutionSnapshot(sql, GetParameterSnapshot(clone),
            includeDebugSql ? clone.ToDebugSql(sql) : null);
    }

    /// <summary>
    /// 创建查询计划的执行快照。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <returns>查询计划对应的 SQL 与参数快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(SqlQueryPlan plan)
        => CreateExecutionSnapshot(plan, false);

    /// <summary>
    /// 创建查询计划的执行快照，并按需生成调试 SQL。
    /// </summary>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="includeDebugSql">是否同时生成调试 SQL。</param>
    /// <returns>查询计划对应的 SQL、参数与可选调试 SQL 快照。</returns>
    public static SqlBuilderExecutionSnapshot CreateExecutionSnapshot(SqlQueryPlan plan, bool includeDebugSql)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        return CreateExecutionSnapshot(plan.GetBuilder(), includeDebugSql);
    }

    /// <summary>
    /// 校验查询计划是否需要写入数据源权限。
    /// </summary>
    /// <param name="plan">待校验的查询计划。</param>
    /// <returns>计划包含 Returning Mutation 时返回 true。</returns>
    public static bool ValidateQueryPlan(SqlQueryPlan plan)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        var builder = plan.GetBuilder();
        if (builder == null || builder.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            return false;
        if (builder is IReturningClauseAccessor { ReturningClause.IsEmpty: false })
            return true;
        throw new InvalidOperationException("Mutation 必须配置 Returning 后才能通过查询结果 API 执行。");
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
        return pager ?? (plan.IsBuilderPlan ? plan.GetBuilder().Pager : null) ??
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
        if (plan.IsBuilderPlan == false)
        {
            var countPlan = SqlQueryPlan.Create($"Select Count(*) From ({plan.CommandText}) As __bing_raw_count",
                plan.Parameters, plan.SplitOn, plan.CommandType);
            countPlan.CopyContextFrom(plan, "Count");
            return countPlan;
        }
        var builder = GetPagingBuilder(plan);
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
        {
            var countPlan = SqlQueryPlan.Create(builder.New().CountAll().From(builder, "t"));
            countPlan.CopyContextFrom(plan, "Count");
            return countPlan;
        }
        builder.ClearSelect();
        var simpleCountPlan = SqlQueryPlan.Create(builder.CountAll());
        simpleCountPlan.CopyContextFrom(plan, "Count");
        return simpleCountPlan;
    }

    /// <summary>
    /// 创建当前页数据查询计划。
    /// </summary>
    /// <param name="plan">原始结构化查询计划。</param>
    /// <param name="pager">本次分页参数。</param>
    /// <returns>应用排序和分页后的数据查询计划。</returns>
    public static SqlQueryPlan CreatePagePlan(SqlQueryPlan plan, IPager pager)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (pager == null)
            throw new ArgumentNullException(nameof(pager));
        if (plan.IsBuilderPlan == false)
            throw new ArgumentException("原生 SQL 分页必须通过 Provider Builder 创建分页计划。", nameof(plan));
        var builder = GetPagingBuilder(plan);
        builder.OrderBy(pager.Order);
        var pagePlan = SqlQueryPlan.Create(builder.Page(pager), plan.SplitOn);
        pagePlan.CopyContextFrom(plan, "Data");
        return pagePlan;
    }

    /// <summary>
    /// 创建原生 SQL 文本的安全分页计划。
    /// </summary>
    /// <param name="plan">原生 SQL 文本计划。</param>
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
        if (plan.IsBuilderPlan)
            return CreatePagePlan(plan, pager);
        if (SqlProviderCapabilityResolver.GetProfile(pageBuilder.Provider).Query.Pagination !=
            SqlQueryCapabilityState.Supported)
            throw new NotSupportedException("当前 SQL Provider 不支持原生 SQL 自动分页。");

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

    private static ISqlBuilder GetPagingBuilder(SqlQueryPlan plan) =>
        CloneExecutionBuilder(plan.GetBuilder());

    private static ISqlBuilder CloneExecutionBuilder(ISqlBuilder builder)
        => builder is SqlBuilderBase sqlBuilder
            ? sqlBuilder.CreateExecutionBuilderSnapshot()
            : builder.Clone();

    private static IReadOnlyCollection<SqlParam> GetParameterSnapshot(ISqlBuilder builder)
    {
        if (builder is not ISqlCommonPartAccessor accessor)
            return builder.GetParams().Select(item => new SqlParam(item.Key, item.Value)).ToArray();
        var parameterManager = accessor.ParameterManager;
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

    private static bool IsSafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var segments = value.Split('.');
        return segments.All(segment => segment.Length > 0 &&
            (char.IsLetter(segment[0]) || segment[0] == '_') &&
            segment.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_'));
    }

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
    public static void FromSubquery<TProjection>(ISqlBuilder builder, SqlSubquery<TProjection> subquery)
        where TProjection : class
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        ((FromClause)((ISqlQueryClauseAccessor)builder).FromClause).From(subquery);
    }

    private sealed class SqlRawPagingParameterMap : ISqlParameterMap
    {
        private readonly IReadOnlyCollection<SqlParameterMapItem> _items;

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

        public object Source { get; }

        public IReadOnlyCollection<SqlParameterMapItem> GetItems() => _items;

        private static IEnumerable<SqlParameterMapItem> CreateItems(string offsetName, int offset, string limitName,
            int limit)
        {
            yield return CreateItem(offsetName, offset);
            yield return CreateItem(limitName, limit);
        }

        private static SqlParameterMapItem CreateItem(string name, int value) => new()
        {
            Name = name,
            Value = value,
            HasExplicitValue = true,
            ValueResolved = true
        };
    }
}