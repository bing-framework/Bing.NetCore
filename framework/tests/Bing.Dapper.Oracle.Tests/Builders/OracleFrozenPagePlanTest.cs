using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证纯 IR 冻结计划派生分页时重绑参数且不改变原计划。</summary>
public sealed class OracleFrozenPagePlanTest
{
    /// <summary>简单冻结计划派生分页应保留 Oracle 排序并替换分页参数。</summary>
    [Fact]
    public void FrozenSimplePage_ShouldRebindParametersFromIrAndKeepSource()
    {
        const string expectedSql =
            "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0 \r\nOrder By \"Name\" \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        var builder = CreateOracleBuilder().Select("Name").From("samples")
            .Where("Name", "one").OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        AssertOraclePage(plan, builder, expectedSql, "5", "2");

        var pureIr = SqlQueryPlan.Create(plan.QueryIr, plan.SplitOn);
        pureIr.CopyContextFrom(plan, "Data");
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(pureIr, new Pager(3, 4));

        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        AssertOraclePage(page, null, expectedSql, "8", "4");
        AssertOraclePage(plan, builder, expectedSql, "5", "2");
    }

    /// <summary>分组冻结计划派生分页应保留分组和 Oracle Offset/Fetch SQL。</summary>
    [Fact]
    public void FrozenGroupedPage_ShouldRebindParametersFromIrAndKeepSource()
    {
        const string expectedSql =
            "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0 \r\nGroup By \"Name\" \r\nOrder By \"Name\" \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        var builder = CreateOracleBuilder().Select("Name").From("samples")
            .Where("Name", "one").GroupBy("Name").OrderBy("Name").Skip(5).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        AssertOraclePage(plan, builder, expectedSql, "5", "2");

        var pureIr = SqlQueryPlan.Create(plan.QueryIr, plan.SplitOn);
        pureIr.CopyContextFrom(plan, "Data");
        var page = SqlBuilderRuntimeBridge.CreatePagePlan(pureIr, new Pager(3, 4));

        Assert.False(page.IsBuilderPlan);
        Assert.Null(page.PlanningBuilder);
        AssertOraclePage(page, null, expectedSql, "8", "4");
        AssertOraclePage(plan, builder, expectedSql, "5", "2");
    }

    private static OracleBuilder CreateOracleBuilder() => new(new SqlBuilderServices(options: new SqlOptions
    {
        QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
    }));

    private static void AssertOraclePage(SqlQueryPlan plan, ISqlBuilder builder, string expectedSql,
        string expectedOffset, string expectedLimit)
    {
        Assert.Equal(expectedSql, plan.CommandText);
        if (builder != null)
            Assert.Equal(expectedSql, builder.ToSql());
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new[] { ":p_0", ":p_1", ":p_2" }, parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "one", expectedOffset, expectedLimit },
            parameters.Select(parameter => parameter.Value));
    }
}
