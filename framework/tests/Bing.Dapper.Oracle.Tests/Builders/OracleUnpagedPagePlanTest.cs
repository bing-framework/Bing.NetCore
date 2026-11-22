using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证未分页 Oracle 冻结查询可从纯 IR 派生 Offset/Fetch 页面。</summary>
public sealed class OracleUnpagedPagePlanTest
{
    /// <summary>普通与分组纯 IR 分页保留 Oracle SQL、参数及源计划。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnpagedFrozenPage_FromPureIr_ShouldAppendOffsetFetchAndKeepSource(bool grouped)
    {
        var builder = CreateOracleBuilder().Select("Name").From("samples").Where("Status", "active");
        if (grouped)
            builder.GroupBy("Name");
        builder.OrderBy("Name");

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        var expectedSourceSql = ExpectedSourceSql(grouped);
        var expectedPageSql = ExpectedPageSql(grouped);
        var expectedCountSql = ExpectedCountSql(grouped);

        Assert.Null(source.PlanningBuilder);
        Assert.Equal(expectedSourceSql, source.CommandText);
        AssertParameters(source, "active");

        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4));

        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        Assert.Equal(expectedPageSql, page.CommandText);
        AssertParameters(page, "active", "8", "4");

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2));
        Assert.Equal(expectedPageSql, repeated.CommandText);
        AssertParameters(repeated, "active", "2", "2");

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal(expectedCountSql, count.CommandText);
        AssertParameters(count, "active");

        Assert.Equal(expectedSourceSql, source.CommandText);
        AssertParameters(source, "active");
    }

    private static OracleBuilder CreateOracleBuilder() => new(new SqlBuilderServices(options: new SqlOptions
    {
        QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
    }));

    private static string ExpectedSourceSql(bool grouped)
    {
        var group = grouped ? " \r\nGroup By \"Name\"" : string.Empty;
        return $"Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0{group} \r\nOrder By \"Name\"";
    }

    private static string ExpectedPageSql(bool grouped) =>
        $"{ExpectedSourceSql(grouped)} \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";

    private static string ExpectedCountSql(bool grouped)
    {
        const string fromAndFilter = "From \"samples\" \r\nWhere \"Status\"=:p_0";
        if (grouped)
            return "Select Count(*) \r\nFrom (Select \"Name\" \r\n" +
                $"{fromAndFilter} \r\nGroup By \"Name\") \"t\"";
        return $"Select Count(*) \r\n{fromAndFilter}";
    }

    private static void AssertParameters(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        var names = values.Length switch
        {
            1 => new[] { ":p_0" },
            3 => new[] { ":p_0", ":p_1", ":p_2" },
            _ => throw new ArgumentOutOfRangeException(nameof(values))
        };
        Assert.Equal(names, parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
