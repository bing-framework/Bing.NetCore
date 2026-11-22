using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle 的 CTE 冻结计划可派生 Count、重复 Page 并隔离源参数。</summary>
public sealed class OracleCteIrTest
{
    public static IEnumerable<object[]> CteCases()
    {
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var sourceState in new[] { "unpaged-unordered", "unpaged-ordered", "paged-ordered" })
            yield return new object[] { shape, sourceState };
    }

    /// <summary>Oracle 将 CTE 留在 Count 包装外层并以 Offset/Fetch 从纯冻结 IR 派生页面。</summary>
    [Theory]
    [MemberData(nameof(CteCases))]
    public void CtePageAndCount_FromFrozenIr_ShouldPreserveOracleSql(string shape, string sourceState)
    {
        var builder = CreateOracleBuilder();
        var cte = builder.New().Select("Name").From("cte_source").Where("Name", "cte-value");
        var sortName = shape == "aggregate" ? "Total" : "Name";

        if (shape == "aggregate")
            builder.CountAll("Total");
        else
        {
            if (shape == "distinct")
                builder.Distinct();
            builder.Select("Name");
        }

        builder.From("selected").Where("Name", "main-value");
        if (shape == "group")
            builder.GroupBy("Name");
        if (sourceState != "unpaged-unordered")
            builder.OrderBy(sortName);
        if (sourceState == "paged-ordered")
            builder.Skip(5).Take(2);
        builder.With("selected", cte);

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var cteParameter = Parameter(sourceState == "paged-ordered" ? 3 : 1);
        var mainParameter = Parameter(0);
        var sourceOrder = sourceState == "unpaged-unordered"
            ? string.Empty
            : $" \r\nOrder By {Quote(sortName)}";
        var sourcePage = sourceState == "paged-ordered"
            ? $" \r\n{PageSql(Parameter(1), Parameter(2))}"
            : string.Empty;
        var sourceSql = CteSql(cteParameter) + "\r\n" +
            MainSql(shape, mainParameter, sourceOrder, sourcePage);

        Assert.False(frozen.IsBuilderPlan);
        Assert.Null(frozen.PlanningBuilder);
        Assert.False(source.IsBuilderPlan);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, sourceState);

        var requestedOrder = sourceState == "unpaged-unordered" ? sortName : null;
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, requestedOrder));
        var offsetParameter = Parameter(sourceState == "paged-ordered" ? 1 : 2);
        var limitParameter = Parameter(sourceState == "paged-ordered" ? 2 : 3);
        var expectedPageSql = CteSql(cteParameter) + "\r\n" +
            MainSql(shape, mainParameter, $" \r\nOrder By {Quote(sortName)}",
                $" \r\n{PageSql(offsetParameter, limitParameter)}");
        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(expectedPageSql, page.CommandText);
        AssertPageParameters(page, sourceState, 8, 4);

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        Assert.False(repeated.IsBuilderPlan);
        Assert.Null(repeated.PlanningBuilder);
        Assert.Equal(expectedPageSql, repeated.CommandText);
        AssertPageParameters(repeated, sourceState, 2, 2);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.False(count.IsBuilderPlan);
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(CteSql(cteParameter) + "\r\n" + CountSql(shape, mainParameter), count.CommandText);
        AssertCountParameters(count, sourceState);

        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, sourceState);
    }

    /// <summary>Oracle 分页能力关闭时，含 CTE 的纯冻结计划应直接拒绝新增分页。</summary>
    [Fact]
    public void CtePage_WhenPaginationIsUnsupported_ShouldRejectAndKeepSource()
    {
        var builder = CreateOracleBuilder(SqlQueryCapabilityState.Unsupported)
            .Select("Name").From("selected").Where("Name", "main-value");
        var cte = builder.New().Select("Name").From("cte_source").Where("Name", "cte-value");
        builder.With("selected", cte).OrderBy("Name");

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = frozen;
        var sourceSql = CteSql(Parameter(1)) + "\r\n" +
            MainSql("simple", Parameter(0), $" \r\nOrder By {Quote("Name")}", string.Empty);

        Assert.NotNull(source.PlanningBuilder);
        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name")));

        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, "unpaged-ordered");
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

    private static string CteSql(string parameter) =>
        $"With {Quote("selected")} \r\nAs (Select {Quote("Name")} \r\nFrom {Quote("cte_source")} \r\n" +
        $"Where {Quote("Name")}={parameter})";

    private static string MainSql(string shape, string mainParameter, string order, string page)
    {
        var select = shape switch
        {
            "distinct" => $"Select Distinct {Quote("Name")}",
            "aggregate" => $"Select Count(*) {Quote("Total")}",
            _ => $"Select {Quote("Name")}"
        };
        var group = shape == "group" ? $" \r\nGroup By {Quote("Name")}" : string.Empty;
        return $"{select} \r\nFrom {Quote("selected")} \r\n" +
            $"Where {Quote("Name")}={mainParameter}{group}{order}{page}";
    }

    private static string CountSql(string shape, string mainParameter)
    {
        var fromAndFilter = $"From {Quote("selected")} \r\nWhere {Quote("Name")}={mainParameter}";
        if (shape == "simple")
            return $"Select Count(*) \r\n{fromAndFilter}";

        var select = shape switch
        {
            "distinct" => $"Select Distinct {Quote("Name")}",
            "aggregate" => $"Select Count(*) {Quote("Total")}",
            _ => $"Select {Quote("Name")}"
        };
        var group = shape == "group" ? $" \r\nGroup By {Quote("Name")}" : string.Empty;
        return $"Select Count(*) \r\nFrom ({select} \r\n{fromAndFilter}{group}) {Quote("t")}";
    }

    private static string PageSql(string offset, string limit) =>
        $"Offset {offset} Rows Fetch Next {limit} Rows Only";

    private static string Quote(string name) => $"\"{name}\"";

    private static string Parameter(int index) => $":p_{index}";

    private static void AssertSourceParameters(SqlQueryPlan plan, string sourceState)
    {
        var names = sourceState == "paged-ordered"
            ? new[] { Parameter(0), Parameter(1), Parameter(2), Parameter(3) }
            : new[] { Parameter(0), Parameter(1) };
        var values = sourceState == "paged-ordered"
            ? new object[] { "main-value", "5", "2", "cte-value" }
            : new object[] { "main-value", "cte-value" };
        AssertParameters(plan, names, values);
    }

    private static void AssertPageParameters(SqlQueryPlan plan, string sourceState, int offset, int limit)
    {
        var names = new[] { Parameter(0), Parameter(1), Parameter(2), Parameter(3) };
        var values = sourceState == "paged-ordered"
            ? new object[] { "main-value", offset.ToString(), limit.ToString(), "cte-value" }
            : new object[] { "main-value", "cte-value", offset.ToString(), limit.ToString() };
        AssertParameters(plan, names, values);
    }

    private static void AssertCountParameters(SqlQueryPlan plan, string sourceState)
    {
        var names = sourceState == "paged-ordered"
            ? new[] { Parameter(0), Parameter(3) }
            : new[] { Parameter(0), Parameter(1) };
        AssertParameters(plan, names, new object[] { "main-value", "cte-value" });
    }

    private static void AssertParameters(SqlQueryPlan plan, string[] names, object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
