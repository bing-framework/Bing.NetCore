using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>验证默认 Provider 的 CTE 查询可从冻结 IR 派生 Count 和 Page。</summary>
public sealed class SqlQueryCteIrProviderTest
{
    public static IEnumerable<object[]> ProviderCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var shape in new[] { "simple", "group", "distinct", "aggregate" })
        foreach (var sourceState in new[] { "unpaged-unordered", "unpaged-ordered", "paged-ordered" })
            yield return new object[] { provider, shape, sourceState };
    }

    /// <summary>CTE 前缀留在外层，重复分页后的 Count 仍只包装主查询并保留原参数。</summary>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void CtePageAndCount_FromFrozenIr_ShouldPreserveProviderSql(string provider, string shape,
        string sourceState)
    {
        var builder = CreateBuilder(provider);
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
        string QuoteName(string name) => Quote(provider, name);
        var mainParameter = Parameter(provider, 0);
        var cteParameter = Parameter(provider, sourceState == "paged-ordered" ? 3 : 1);
        var sourceOrder = sourceState == "unpaged-unordered"
            ? string.Empty
            : $" \r\nOrder By {QuoteName(sortName)}";
        var sourcePage = sourceState == "paged-ordered"
            ? $" \r\n{PageSql(provider, Parameter(provider, 1), Parameter(provider, 2))}"
            : string.Empty;
        var sourceSql = CteSql(provider, cteParameter) + "\r\n" +
            MainSql(provider, shape, mainParameter, sourceOrder, sourcePage);

        Assert.False(frozen.IsBuilderPlan);
        Assert.Null(frozen.PlanningBuilder);
        Assert.False(source.IsBuilderPlan);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, provider, sourceState);

        var requestedOrder = sourceState == "unpaged-unordered" ? sortName : null;
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, requestedOrder));
        var offsetParameter = Parameter(provider, sourceState == "paged-ordered" ? 1 : 2);
        var limitParameter = Parameter(provider, sourceState == "paged-ordered" ? 2 : 3);
        var expectedPageSql = CteSql(provider, cteParameter) + "\r\n" +
            MainSql(provider, shape, mainParameter, $" \r\nOrder By {QuoteName(sortName)}",
                $" \r\n{PageSql(provider, offsetParameter, limitParameter)}");
        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(expectedPageSql, page.CommandText);
        AssertPageParameters(page, provider, sourceState, 8, 4);

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        Assert.False(repeated.IsBuilderPlan);
        Assert.Null(repeated.PlanningBuilder);
        Assert.Equal(expectedPageSql, repeated.CommandText);
        AssertPageParameters(repeated, provider, sourceState, 2, 2);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.False(count.IsBuilderPlan);
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(CteSql(provider, cteParameter) + "\r\n" + CountSql(provider, shape, mainParameter),
            count.CommandText);
        AssertCountParameters(count, provider, sourceState);

        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, provider, sourceState);
    }

    /// <summary>冻结 CTE 计划仍须遵守 Provider 显式关闭的分页能力。</summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("postgresql")]
    [InlineData("sqlite")]
    public void CtePage_WhenPaginationIsUnsupported_ShouldRejectAndKeepSource(string provider)
    {
        var builder = CreateBuilder(provider, SqlQueryCapabilityState.Unsupported)
            .Select("Name").From("selected").Where("Name", "main-value");
        var cte = builder.New().Select("Name").From("cte_source").Where("Name", "cte-value");
        builder.With("selected", cte).OrderBy("Name");

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = frozen;
        var sourceSql = CteSql(provider, Parameter(provider, 1)) + "\r\n" +
            MainSql(provider, "simple", Parameter(provider, 0), $" \r\nOrder By {Quote(provider, "Name")}", string.Empty);

        Assert.NotNull(source.PlanningBuilder);
        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name")));

        Assert.Equal(sourceSql, source.CommandText);
        AssertSourceParameters(source, provider, "unpaged-ordered");
    }

    private static ISqlBuilder CreateBuilder(string provider,
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
        return provider switch
        {
            "sqlserver" => new SqlServerBuilder(services),
            "mysql" => new MySqlBuilder(services),
            "postgresql" => new PostgreSqlBuilder(services),
            "sqlite" => new SqliteBuilder(services),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }

    private static string CteSql(string provider, string parameter) =>
        $"{CteKeyword(provider)} {Quote(provider, "selected")} \r\n" +
        $"As (Select {Quote(provider, "Name")} \r\nFrom {Quote(provider, "cte_source")} \r\n" +
        $"Where {Quote(provider, "Name")}={parameter})";

    private static string MainSql(string provider, string shape, string mainParameter, string order, string page)
    {
        string QuoteName(string name) => Quote(provider, name);
        var select = shape switch
        {
            "distinct" => $"Select Distinct {QuoteName("Name")}",
            "aggregate" => $"Select Count(*) As {QuoteName("Total")}",
            _ => $"Select {QuoteName("Name")}"
        };
        var group = shape == "group" ? $" \r\nGroup By {QuoteName("Name")}" : string.Empty;
        return $"{select} \r\nFrom {QuoteName("selected")} \r\n" +
            $"Where {QuoteName("Name")}={mainParameter}{group}{order}{page}";
    }

    private static string CountSql(string provider, string shape, string mainParameter)
    {
        string QuoteName(string name) => Quote(provider, name);
        var fromAndFilter = $"From {QuoteName("selected")} \r\nWhere {QuoteName("Name")}={mainParameter}";
        if (shape == "simple")
            return $"Select Count(*) \r\n{fromAndFilter}";

        var select = shape switch
        {
            "distinct" => $"Select Distinct {QuoteName("Name")}",
            "aggregate" => $"Select Count(*) As {QuoteName("Total")}",
            _ => $"Select {QuoteName("Name")}"
        };
        var group = shape == "group" ? $" \r\nGroup By {QuoteName("Name")}" : string.Empty;
        return $"Select Count(*) \r\nFrom ({select} \r\n{fromAndFilter}{group}) As {QuoteName("t")}";
    }

    private static string CteKeyword(string provider) => provider == "sqlserver" ? "With" : "With Recursive";

    private static string PageSql(string provider, string offset, string limit) => provider == "sqlserver"
        ? $"Offset {offset} Rows Fetch Next {limit} Rows Only"
        : $"Limit {limit} OFFSET {offset}";

    private static string Quote(string provider, string name) => provider switch
    {
        "sqlserver" => $"[{name}]",
        "postgresql" => $"\"{name}\"",
        _ => $"`{name}`"
    };

    private static string Parameter(string provider, int index) => $"@_p_{index}";

    private static void AssertSourceParameters(SqlQueryPlan plan, string provider, string sourceState)
    {
        var names = sourceState == "paged-ordered"
            ? new[] { Parameter(provider, 0), Parameter(provider, 1), Parameter(provider, 2), Parameter(provider, 3) }
            : new[] { Parameter(provider, 0), Parameter(provider, 1) };
        var values = sourceState == "paged-ordered"
            ? new object[] { "main-value", 5, 2, "cte-value" }
            : new object[] { "main-value", "cte-value" };
        AssertParameters(plan, names, values);
    }

    private static void AssertPageParameters(SqlQueryPlan plan, string provider, string sourceState,
        int offset, int limit)
    {
        var names = new[] { Parameter(provider, 0), Parameter(provider, 1), Parameter(provider, 2), Parameter(provider, 3) };
        var values = sourceState == "paged-ordered"
            ? new object[] { "main-value", offset, limit, "cte-value" }
            : new object[] { "main-value", "cte-value", offset, limit };
        AssertParameters(plan, names, values);
    }

    private static void AssertCountParameters(SqlQueryPlan plan, string provider, string sourceState)
    {
        var names = sourceState == "paged-ordered"
            ? new[] { Parameter(provider, 0), Parameter(provider, 3) }
            : new[] { Parameter(provider, 0), Parameter(provider, 1) };
        AssertParameters(plan, names, new object[] { "main-value", "cte-value" });
    }

    private static void AssertParameters(SqlQueryPlan plan, string[] names, object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
