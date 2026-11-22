using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle Skip-only 冻结计划可由查询 IR 派生 Count 和分页。</summary>
public sealed class OracleSkipOnlyIrTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SkipOnlyFrozenIr_ShouldCountAndPageWithoutBuilderFallback(bool ordered)
    {
        var builder = CreateOracleBuilder().Select("Name").From("samples").Where("Status", "active");
        if (ordered)
            builder.OrderBy("Name");
        builder.Skip(5);

        var frozen = SqlQueryPlan.Create(builder).Freeze();
        var source = SqlQueryPlan.Create(frozen.QueryIr, frozen.SplitOn);
        Assert.Null(source.PlanningBuilder);

        const string sourceBody = "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0";
        var sourceSql = sourceBody +
            (ordered ? " \r\nOrder By \"Name\"" : string.Empty);
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active", "5");

        var countSql = "Select Count(*) \r\nFrom \"samples\" \r\nWhere \"Status\"=:p_0";
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        Assert.Equal(countSql, count.CommandText);
        AssertParameters(count, "active");

        var order = ordered ? null : "Name";
        var pageSql = sourceBody + " \r\nOrder By \"Name\"" +
            " \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, order));
        Assert.Equal(pageSql, page.CommandText);
        AssertParameters(page, "active", "8", "4");

        var repeated = SqlBuilderRuntimeBridge.CreatePagePlan(page, new Pager(2, 2, order));
        Assert.Equal(pageSql, repeated.CommandText);
        AssertParameters(repeated, "active", "2", "2");

        var pageCount = SqlBuilderRuntimeBridge.CreateCountPlan(repeated);
        Assert.Equal(countSql, pageCount.CommandText);
        AssertParameters(pageCount, "active");
        Assert.Equal(sourceSql, source.CommandText);
        AssertParameters(source, "active", "5");
    }

    private static OracleBuilder CreateOracleBuilder() => new(new SqlBuilderServices(options: new SqlOptions
    {
        QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
    }));

    private static void AssertParameters(SqlQueryPlan plan, params object[] values)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(Enumerable.Range(0, values.Length).Select(index => $":p_{index}"),
            parameters.Select(parameter => parameter.Name));
        Assert.Equal(values, parameters.Select(parameter => parameter.Value));
    }
}
