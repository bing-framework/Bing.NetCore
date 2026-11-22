using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle 集合查询可从冻结 IR 派生 Count 和 Page。</summary>
public sealed class OracleSetIrTest
{
    public static IEnumerable<object[]> SupportedCases()
    {
        foreach (var operation in new[] { "Union", "Union All", "Intersect" })
        foreach (var cte in new[] { false, true })
        foreach (var paged in new[] { false, true })
            yield return new object[] { operation, cte, paged };
    }

    /// <summary>Oracle 的完整集合 SQL 和参数在 Count/Page 派生后保持原有布局及源隔离。</summary>
    [Theory]
    [MemberData(nameof(SupportedCases))]
    public void SetPageAndCount_FromFrozenIr_ShouldPreserveOracleSql(string operation, bool cte, bool paged)
    {
        var frozen = CreateFrozenPlan(operation, cte, paged);
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var expectedSource = ExpectedSource(operation, cte, paged);

        Assert.Null(frozen.PlanningBuilder);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(expectedSource, source.CommandText);
        AssertParameters(source, SourceNames(cte, paged), SourceValues(cte, paged));

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(ExpectedPage(operation, cte, paged), page.CommandText);
        AssertParameters(page, PageNames(cte), PageValues(cte, paged));

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        var expectedCount = CtePrefix(cte, CteParameter(cte, paged)) +
            CountSql(SetBody(operation, cte, Parameter(0), Parameter(BranchIndex(cte, paged))));
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(expectedCount, count.CommandText);
        AssertParameters(count, CountNames(cte, paged), CountValues(cte));

        Assert.Equal(expectedSource, source.CommandText);
        AssertParameters(source, SourceNames(cte, paged), SourceValues(cte, paged));
    }

    /// <summary>Oracle Provider 不支持 Except，选项不能重新启用该操作。</summary>
    [Fact]
    public void Except_WhenRendered_ShouldBeRejectedByProvider()
    {
        var builder = CreateOracleBuilder();
        builder.Select("Name").From("samples").Where("Status", "root-value").OrderBy("Name");
        var source = SqlQueryPlan.Create(builder).Freeze();
        builder.Except(builder.New().Select("Name").From("archive_rows"));

        Assert.Throws<NotSupportedException>(() => builder.ToSql());
        Assert.Null(source.PlanningBuilder);
        Assert.Equal("Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0 \r\nOrder By \"Name\"",
            source.CommandText);
        AssertParameters(source, new[] { Parameter(0) }, new object[] { "root-value" });
    }

    /// <summary>关闭 Oracle 分页能力时，集合源和 Builder 回退保持不变。</summary>
    [Fact]
    public void SetPage_WhenPaginationIsUnsupported_ShouldKeepSourceAndFallback()
    {
        var frozen = CreateFrozenPlan("Union All", cte: true, paged: false,
            SqlQueryCapabilityState.Unsupported);
        var sourceSql = ExpectedSource("Union All", cte: true, paged: false);

        Assert.NotNull(frozen.PlanningBuilder);
        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(2, 3)));

        Assert.NotNull(frozen.PlanningBuilder);
        Assert.Equal(sourceSql, frozen.CommandText);
        AssertParameters(frozen, SourceNames(cte: true, paged: false),
            SourceValues(cte: true, paged: false));
    }

    private static SqlQueryPlan CreateFrozenPlan(string operation, bool cte, bool paged,
        SqlQueryCapabilityState pagination = SqlQueryCapabilityState.Supported)
    {
        var builder = CreateOracleBuilder(pagination);
        builder.Select("Name").From(cte ? "selected" : "samples").Where("Status", "root-value");
        if (cte)
        {
            var cteBuilder = builder.New().Select("Name").From("cte_source").Where("Status", "cte-value");
            builder.With("selected", cteBuilder);
        }

        var branch = builder.New().Select("Name").From("archive_rows").Where("Status", "branch-value");
        Apply(builder, branch, operation);
        builder.OrderBy("Name");
        if (paged)
            builder.Skip(5).Take(2);
        return SqlQueryPlan.Create(builder).Freeze();
    }

    private static OracleBuilder CreateOracleBuilder(
        SqlQueryCapabilityState pagination = SqlQueryCapabilityState.Supported)
    {
        var services = new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities
            {
                Cte = SqlQueryCapabilityState.Supported,
                Pagination = pagination
            }
        });
        return new OracleBuilder(services);
    }

    private static void Apply(ISqlBuilder root, ISqlBuilder branch, string operation)
    {
        switch (operation)
        {
            case "Union": root.Union(branch); break;
            case "Union All": root.UnionAll(branch); break;
            case "Intersect": root.Intersect(branch); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static string ExpectedSource(string operation, bool cte, bool paged)
    {
        var prefix = CtePrefix(cte, CteParameter(cte, paged));
        var body = SetBody(operation, cte, Parameter(0), Parameter(BranchIndex(cte, paged)));
        var order = $" \r\nOrder By {Quote("Name")}";
        var page = paged ? $" \r\n{PageSql(Parameter(1), Parameter(2))}" : string.Empty;
        return prefix + body + order + page;
    }

    private static string ExpectedPage(string operation, bool cte, bool paged)
    {
        var prefix = CtePrefix(cte, CteParameter(cte, paged));
        var body = SetBody(operation, cte, Parameter(0), Parameter(BranchIndex(cte, paged)));
        var offsetIndex = paged ? 1 : cte ? 3 : 2;
        var limitIndex = offsetIndex + 1;
        return prefix + body + $" \r\nOrder By {Quote("Name")} \r\n" +
            PageSql(Parameter(offsetIndex), Parameter(limitIndex));
    }

    private static string SetBody(string operation, bool cte, string rootParameter, string branchParameter)
    {
        var root = SelectBody(cte ? "selected" : "samples", rootParameter);
        var branch = SelectBody("archive_rows", branchParameter);
        return $"({root} \r\n) \r\n{operation} \r\n({branch} \r\n)";
    }

    private static string SelectBody(string table, string parameter) =>
        $"Select {Quote("Name")} \r\nFrom {Quote(table)} \r\n" +
        $"Where {Quote("Status")}={parameter}";

    private static string CountSql(string setBody) =>
        $"Select Count(*) \r\nFrom ({setBody}) {Quote("t")}";

    private static string CtePrefix(bool cte, string parameter) => !cte
        ? string.Empty
        : $"With {Quote("selected")} \r\nAs (Select {Quote("Name")} \r\nFrom {Quote("cte_source")} \r\n" +
            $"Where {Quote("Status")}={parameter})\r\n";

    private static string PageSql(string offset, string limit) =>
        $"Offset {offset} Rows Fetch Next {limit} Rows Only";

    private static string Quote(string name) => $"\"{name}\"";

    private static string Parameter(int index) => $":p_{index}";

    private static string CteParameter(bool cte, bool paged) => cte ? Parameter(paged ? 3 : 1) : null;

    private static int BranchIndex(bool cte, bool paged) => paged
        ? cte ? 4 : 3
        : cte ? 2 : 1;

    private static string[] SourceNames(bool cte, bool paged) =>
        Enumerable.Range(0, paged ? cte ? 5 : 4 : cte ? 3 : 2)
            .Select(Parameter).ToArray();

    private static object[] SourceValues(bool cte, bool paged)
    {
        if (paged)
            return cte
                ? new object[] { "root-value", "5", "2", "cte-value", "branch-value" }
                : new object[] { "root-value", "5", "2", "branch-value" };
        return cte
            ? new object[] { "root-value", "cte-value", "branch-value" }
            : new object[] { "root-value", "branch-value" };
    }

    private static string[] PageNames(bool cte) =>
        Enumerable.Range(0, cte ? 5 : 4)
            .Select(Parameter).ToArray();

    private static object[] PageValues(bool cte, bool paged)
    {
        if (paged)
            return cte
                ? new object[] { "root-value", "8", "4", "cte-value", "branch-value" }
                : new object[] { "root-value", "8", "4", "branch-value" };
        return cte
            ? new object[] { "root-value", "cte-value", "branch-value", "8", "4" }
            : new object[] { "root-value", "branch-value", "8", "4" };
    }

    private static string[] CountNames(bool cte, bool paged)
    {
        if (!paged)
            return Enumerable.Range(0, cte ? 3 : 2).Select(Parameter).ToArray();
        return cte
            ? new[] { Parameter(0), Parameter(3), Parameter(4) }
            : new[] { Parameter(0), Parameter(3) };
    }

    private static object[] CountValues(bool cte) => cte
        ? new object[] { "root-value", "cte-value", "branch-value" }
        : new object[] { "root-value", "branch-value" };

    private static void AssertParameters(SqlQueryPlan plan, string[] names, object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
