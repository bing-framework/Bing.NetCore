using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>验证未分页查询的纯 IR 可派生 Provider 分页计划。</summary>
public sealed class SqlQueryUnpagedPageIrProviderTest
{
    public static IEnumerable<object[]> UnpagedProviderPageCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var grouped in new[] { false, true })
            yield return new object[] { provider, grouped };
    }

    /// <summary>未分页冻结查询从纯 IR 派生分页后保留 Provider SQL、参数及源计划。</summary>
    [Theory]
    [MemberData(nameof(UnpagedProviderPageCases))]
    public void UnpagedFrozenPage_FromPureIr_ShouldAppendProviderPagingAndKeepSource(
        string provider, bool grouped)
    {
        var builder = CreateBuilder(provider).Select("Name").From("samples").Where("Status", "active");
        if (grouped)
            builder.GroupBy("Name");
        builder.OrderBy("Name");

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var expectedSourceSql = ExpectedSourceSql(provider, grouped);
        var expectedPageSql = ExpectedPageSql(provider, grouped);
        var expectedCountSql = ExpectedCountSql(provider, grouped);

        Assert.Null(source.PlanningBuilder);
        Assert.Equal(expectedSourceSql, source.CommandText);
        AssertParameters(source, "active");

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(expectedPageSql, page.CommandText);
        AssertParameters(page, "active", 8, 4);

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        Assert.Equal(expectedPageSql, repeated.CommandText);
        AssertParameters(repeated, "active", 2, 2);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal(expectedCountSql, count.CommandText);
        AssertParameters(count, "active");

        Assert.Equal(expectedSourceSql, source.CommandText);
        AssertParameters(source, "active");
    }

    private static ISqlBuilder CreateBuilder(string provider)
    {
        var services = new SqlBuilderServices(options: new SqlOptions
        {
            QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
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

    private static string ExpectedSourceSql(string provider, bool grouped)
    {
        var (open, close) = Quote(provider);
        var group = grouped ? $" \r\nGroup By {open}Name{close}" : string.Empty;
        return $"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0{group} \r\nOrder By {open}Name{close}";
    }

    private static string ExpectedPageSql(string provider, bool grouped)
    {
        var page = provider == "sqlserver"
            ? "Offset @_p_1 Rows Fetch Next @_p_2 Rows Only"
            : "Limit @_p_2 OFFSET @_p_1";
        return $"{ExpectedSourceSql(provider, grouped)} \r\n{page}";
    }

    private static string ExpectedCountSql(string provider, bool grouped)
    {
        var (open, close) = Quote(provider);
        var source = $"Select {open}Name{close} \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0";
        if (grouped)
            return $"Select Count(*) \r\nFrom ({source} \r\nGroup By {open}Name{close}) As {open}t{close}";
        return $"Select Count(*) \r\nFrom {open}samples{close} \r\nWhere {open}Status{close}=@_p_0";
    }

    private static (string Open, string Close) Quote(string provider) => provider switch
    {
        "sqlserver" => ("[", "]"),
        "postgresql" => ("\"", "\""),
        _ => ("`", "`")
    };

    private static void AssertParameters(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        var names = values.Length switch
        {
            1 => new[] { "@_p_0" },
            3 => new[] { "@_p_0", "@_p_1", "@_p_2" },
            _ => throw new ArgumentOutOfRangeException(nameof(values))
        };
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
