using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>验证纯查询 IR 按请求排序派生 Provider 分页计划。</summary>
public sealed class SqlQueryPageOrderIrProviderTest
{
    public static IEnumerable<object[]> PageOrderCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var shape in new[] { "unpaged-unordered", "unpaged-ordered", "paged-ordered" })
            yield return new object[] { provider, shape };
    }

    /// <summary>纯 IR 添加或扩展排序时保持 Provider SQL、分页参数和来源计划。</summary>
    [Theory]
    [MemberData(nameof(PageOrderCases))]
    public void PageOrder_FromPureIr_ShouldPreserveProviderSqlAndSource(string provider, string shape)
    {
        var builder = CreateBuilder(provider).Select("Name").From("samples").Where("Status", "active");
        if (shape != "unpaged-unordered")
            builder.OrderBy("Name");
        if (shape == "paged-ordered")
            builder.Skip(5).Take(2);

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var (open, close) = Quote(provider);
        var sourceOrder = shape == "unpaged-unordered" ? string.Empty : $" \r\nOrder By {open}Name{close}";
        var sourcePage = shape == "paged-ordered" ? $" \r\n{PageSql(provider, "@_p_1", "@_p_2")}" : string.Empty;
        var sourceSql = $"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0{sourceOrder}{sourcePage}";
        Assert.Null(source.PlanningBuilder);
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, shape == "paged-ordered"
            ? new object[] { "active", 5, 2 }
            : new object[] { "active" });

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "Name Desc,Id Desc"));
        var expectedOrder = shape == "unpaged-unordered"
            ? $"{open}Name{close} Desc,{open}Id{close} Desc"
            : $"{open}Name{close},{open}Id{close} Desc";
        var expectedPageSql = $"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0 \r\nOrder By {expectedOrder} \r\n{PageSql(provider, "@_p_1", "@_p_2")}";
        Assert.Equal(expectedPageSql, page.CommandText);
        AssertParameters(page, "active", 8, 4);

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        Assert.Equal(expectedPageSql, repeated.CommandText);
        AssertParameters(repeated, "active", 2, 2);
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal($"Select Count(*) \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0", count.CommandText);
        AssertParameters(count, "active");

        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, shape == "paged-ordered"
            ? new object[] { "active", 5, 2 }
            : new object[] { "active" });
    }

    /// <summary>首次添加排序不能绕过 Provider 禁用分页的能力校验。</summary>
    [Theory]
    [InlineData("sqlserver")]
    [InlineData("mysql")]
    [InlineData("postgresql")]
    [InlineData("sqlite")]
    public void NewOrder_WhenPaginationIsUnsupported_ShouldRejectAndKeepSource(string provider)
    {
        var source = SqlQueryPlan.Create(CreateBuilder(provider, SqlQueryCapabilityState.Unsupported)
            .Select("Name").From("samples").Where("Status", "active")).Freeze();
        var (open, close) = Quote(provider);

        Assert.Throws<NotSupportedException>(() =>
            SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(2, 3, "Name Desc")));

        Assert.Equal($"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0", source.CommandText);
        AssertParameters(source, "active");
    }

    private static ISqlBuilder CreateBuilder(string provider,
        SqlQueryCapabilityState pagination = SqlQueryCapabilityState.Supported)
    {
        var services = new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = pagination }
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

    private static string PageSql(string provider, string offset, string limit) => provider == "sqlserver"
        ? $"Offset {offset} Rows Fetch Next {limit} Rows Only"
        : $"Limit {limit} OFFSET {offset}";

    private static (string Open, string Close) Quote(string provider) => provider switch
    {
        "sqlserver" => ("[", "]"),
        "postgresql" => ("\"", "\""),
        _ => ("`", "`")
    };

    private static void AssertParameters(SqlQueryPlan plan, params object[] expectedValues)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(expectedValues.Length, parameters.Length);
        Assert.Equal(Enumerable.Range(0, expectedValues.Length).Select(index => $"@_p_{index}"),
            parameters.Select(parameter => parameter.Name));
        Assert.Equal(expectedValues, parameters.Select(parameter => parameter.Value));
    }
}
