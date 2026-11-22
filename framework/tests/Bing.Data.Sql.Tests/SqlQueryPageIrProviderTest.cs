using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests;

/// <summary>验证各内置 Provider 可从纯查询 IR 派生分页计划。</summary>
public sealed class SqlQueryPageIrProviderTest
{
    public static IEnumerable<object[]> FrozenProviderPageCases()
    {
        foreach (var provider in new[] { "sqlserver", "mysql", "postgresql", "sqlite" })
        foreach (var grouped in new[] { false, true })
            yield return new object[] { provider, grouped };
    }

    /// <summary>普通和分组查询均沿用 Provider SQL，仅更新冻结分页参数。</summary>
    [Theory]
    [MemberData(nameof(FrozenProviderPageCases))]
    public void FrozenPage_FromPureIr_ShouldKeepProviderSqlAndRebindParameters(string provider, bool grouped)
    {
        var builder = CreateBuilder(provider).Select("Name").From("samples");
        if (grouped)
            builder.GroupBy("Name");
        builder.OrderBy("Name").Skip(5).Take(2);

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var expectedSql = ExpectedSql(provider, grouped);

        Assert.Null(source.PlanningBuilder);
        Assert.Equal(expectedSql, source.CommandText);
        AssertPageParameters(source, 5, 2);

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(expectedSql, page.CommandText);
        AssertPageParameters(page, 8, 4);
        Assert.Equal(expectedSql, source.CommandText);
        AssertPageParameters(source, 5, 2);
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

    private static string ExpectedSql(string provider, bool grouped)
    {
        var (open, close) = provider switch
        {
            "sqlserver" => ("[", "]"),
            "postgresql" => ("\"", "\""),
            _ => ("`", "`")
        };
        string Quote(string name) => open + name + close;
        var group = grouped ? $" \r\nGroup By {Quote("Name")}" : string.Empty;
        var page = provider == "sqlserver"
            ? "Offset @_p_0 Rows Fetch Next @_p_1 Rows Only"
            : "Limit @_p_1 OFFSET @_p_0";
        return $"Select {Quote("Name")} \r\nFrom {Quote("samples")}{group} \r\nOrder By {Quote("Name")} \r\n{page}";
    }

    private static void AssertPageParameters(SqlQueryPlan plan, int offset, int limit)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { offset, limit }, parameters.Select(parameter => parameter.Value));
    }
}
