using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>验证各默认 Provider 的集合查询可从冻结 IR 派生 Count 和 Page。</summary>
public sealed class SqlQuerySetIrProviderTest
{
    public static IEnumerable<object[]> SupportedCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var operation in Operations(provider))
        foreach (var cte in new[] { false, true })
        foreach (var paged in new[] { false, true })
            yield return new object[] { provider, operation, cte, paged };
    }

    /// <summary>集合查询的完整 SQL 和参数在 Count/Page 派生后保持 Provider 布局及源隔离。</summary>
    [Theory]
    [MemberData(nameof(SupportedCases))]
    public void SetPageAndCount_FromFrozenIr_ShouldPreserveProviderSql(
        string provider, string operation, bool cte, bool paged)
    {
        var frozen = CreateFrozenPlan(provider, operation, cte, paged);
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var expectedSource = ExpectedSource(provider, operation, cte, paged);

        Assert.Null(frozen.PlanningBuilder);
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(expectedSource, source.CommandText);
        AssertParameters(source, SourceNames(provider, cte, paged), SourceValues(cte, paged));

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(ExpectedPage(provider, operation, cte, paged), page.CommandText);
        AssertParameters(page, PageNames(provider, cte), PageValues(cte, paged));

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(page);
        var expectedCount = CtePrefix(provider, cte, CteParameter(provider, cte, paged)) +
            CountSql(provider, SetBody(provider, operation, cte,
                Parameter(provider, 0), Parameter(provider, BranchIndex(cte, paged))));
        Assert.Null(count.PlanningBuilder);
        Assert.Equal(expectedCount, count.CommandText);
        AssertParameters(count, CountNames(provider, cte, paged), CountValues(cte));

        Assert.Equal(expectedSource, source.CommandText);
        AssertParameters(source, SourceNames(provider, cte, paged), SourceValues(cte, paged));
    }

    /// <summary>显式关闭分页时，集合源及其 Builder 回退仍保留，派生失败不会改写源计划。</summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("postgresql")]
    [InlineData("sqlite")]
    public void SetPage_WhenPaginationIsUnsupported_ShouldKeepSourceAndFallback(string provider)
    {
        var frozen = CreateFrozenPlan(provider, "Union All", cte: true, paged: false,
            SqlQueryCapabilityState.Unsupported);
        var sourceSql = ExpectedSource(provider, "Union All", cte: true, paged: false);

        Assert.NotNull(frozen.PlanningBuilder);
        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(frozen, new Pager(2, 3)));

        Assert.NotNull(frozen.PlanningBuilder);
        Assert.Equal(sourceSql, frozen.CommandText);
        AssertParameters(frozen, SourceNames(provider, cte: true, paged: false),
            SourceValues(cte: true, paged: false));
    }

    /// <summary>MySQL Provider 不支持 Intersect 和 Except，选项不能重新启用这些操作。</summary>
    [Theory]
    [InlineData("Intersect")]
    [InlineData("Except")]
    public void UnsupportedSetOperation_WhenRendered_ShouldBeRejectedByProvider(string operation)
    {
        var builder = CreateBuilder("mysql", SqlQueryCapabilityState.Supported);
        builder.Select("Name").From("samples").Where("Status", "root-value").OrderBy("Name");
        var source = SqlQueryPlan.Create(builder).Freeze();
        Apply(builder, builder.New().Select("Name").From("archive_rows"), operation);

        Assert.Throws<NotSupportedException>(() => builder.ToSql());
        Assert.Null(source.PlanningBuilder);
        Assert.Equal("Select `Name` \r\nFrom `samples` \r\nWhere `Status`=@_p_0 \r\nOrder By `Name`",
            source.CommandText);
        AssertParameters(source, new[] { Parameter("mysql", 0) }, new object[] { "root-value" });
    }

    private static SqlQueryPlan CreateFrozenPlan(string provider, string operation, bool cte, bool paged,
        SqlQueryCapabilityState pagination = SqlQueryCapabilityState.Supported)
    {
        var builder = CreateBuilder(provider, pagination);
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

    private static ISqlBuilder CreateBuilder(string provider, SqlQueryCapabilityState pagination)
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

    private static IEnumerable<string> Operations(string provider) => provider == "mysql"
        ? new[] { "Union", "Union All" }
        : new[] { "Union", "Union All", "Intersect", "Except" };

    private static void Apply(ISqlBuilder root, ISqlBuilder branch, string operation)
    {
        switch (operation)
        {
            case "Union": root.Union(branch); break;
            case "Union All": root.UnionAll(branch); break;
            case "Intersect": root.Intersect(branch); break;
            case "Except": root.Except(branch); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static string ExpectedSource(string provider, string operation, bool cte, bool paged)
    {
        var prefix = CtePrefix(provider, cte, CteParameter(provider, cte, paged));
        var body = SetBody(provider, operation, cte, Parameter(provider, 0),
            Parameter(provider, BranchIndex(cte, paged)));
        var order = $" \r\nOrder By {Quote(provider, "Name")}";
        var page = paged
            ? $" \r\n{PageSql(provider, Parameter(provider, 1), Parameter(provider, 2))}"
            : string.Empty;
        return prefix + body + order + page;
    }

    private static string ExpectedPage(string provider, string operation, bool cte, bool paged)
    {
        var prefix = CtePrefix(provider, cte, CteParameter(provider, cte, paged));
        var body = SetBody(provider, operation, cte, Parameter(provider, 0),
            Parameter(provider, BranchIndex(cte, paged)));
        var offsetIndex = paged ? 1 : cte ? 3 : 2;
        var limitIndex = offsetIndex + 1;
        return prefix + body + $" \r\nOrder By {Quote(provider, "Name")} \r\n" +
            PageSql(provider, Parameter(provider, offsetIndex), Parameter(provider, limitIndex));
    }

    private static string SetBody(string provider, string operation, bool cte, string rootParameter,
        string branchParameter)
    {
        var root = SelectBody(provider, cte ? "selected" : "samples", rootParameter);
        var branch = SelectBody(provider, "archive_rows", branchParameter);
        return provider == "sqlite"
            ? $"{root} \r\n{operation} \r\n{branch}"
            : $"({root} \r\n) \r\n{operation} \r\n({branch} \r\n)";
    }

    private static string SelectBody(string provider, string table, string parameter) =>
        $"Select {Quote(provider, "Name")} \r\nFrom {Quote(provider, table)} \r\n" +
        $"Where {Quote(provider, "Status")}={parameter}";

    private static string CountSql(string provider, string setBody) =>
        $"Select Count(*) \r\nFrom ({setBody}) As {Quote(provider, "t")}";

    private static string CtePrefix(string provider, bool cte, string parameter)
    {
        if (!cte)
            return string.Empty;
        var keyword = provider == "sqlserver" ? "With" : "With Recursive";
        return $"{keyword} {Quote(provider, "selected")} \r\n" +
            $"As (Select {Quote(provider, "Name")} \r\nFrom {Quote(provider, "cte_source")} \r\n" +
            $"Where {Quote(provider, "Status")}={parameter})\r\n";
    }

    private static string PageSql(string provider, string offset, string limit) =>
        provider == "sqlserver"
            ? $"Offset {offset} Rows Fetch Next {limit} Rows Only"
            : $"Limit {limit} OFFSET {offset}";

    private static string Quote(string provider, string name) => provider switch
    {
        "sqlserver" => $"[{name}]",
        "postgresql" => $"\"{name}\"",
        _ => $"`{name}`"
    };

    private static string Parameter(string provider, int index) => $"@_p_{index}";

    private static string CteParameter(string provider, bool cte, bool paged) =>
        cte ? Parameter(provider, paged ? 3 : 1) : null;

    private static int BranchIndex(bool cte, bool paged) => paged
        ? cte ? 4 : 3
        : cte ? 2 : 1;

    private static string[] SourceNames(string provider, bool cte, bool paged) =>
        Enumerable.Range(0, paged ? cte ? 5 : 4 : cte ? 3 : 2)
            .Select(index => Parameter(provider, index)).ToArray();

    private static object[] SourceValues(bool cte, bool paged)
    {
        if (paged)
            return cte
                ? new object[] { "root-value", 5, 2, "cte-value", "branch-value" }
                : new object[] { "root-value", 5, 2, "branch-value" };
        return cte
            ? new object[] { "root-value", "cte-value", "branch-value" }
            : new object[] { "root-value", "branch-value" };
    }

    private static string[] PageNames(string provider, bool cte) =>
        Enumerable.Range(0, cte ? 5 : 4)
            .Select(index => Parameter(provider, index)).ToArray();

    private static object[] PageValues(bool cte, bool paged)
    {
        if (paged)
            return cte
                ? new object[] { "root-value", 8, 4, "cte-value", "branch-value" }
                : new object[] { "root-value", 8, 4, "branch-value" };
        return cte
            ? new object[] { "root-value", "cte-value", "branch-value", 8, 4 }
            : new object[] { "root-value", "branch-value", 8, 4 };
    }

    private static string[] CountNames(string provider, bool cte, bool paged)
    {
        if (!paged)
            return Enumerable.Range(0, cte ? 3 : 2).Select(index => Parameter(provider, index)).ToArray();
        return cte
            ? new[] { Parameter(provider, 0), Parameter(provider, 3), Parameter(provider, 4) }
            : new[] { Parameter(provider, 0), Parameter(provider, 3) };
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
