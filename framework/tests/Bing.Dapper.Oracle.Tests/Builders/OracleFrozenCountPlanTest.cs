using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle 冻结计划派生计数的 SQL 与参数。</summary>
public sealed class OracleFrozenCountPlanTest
{
    /// <summary>简单冻结计划计数应保留筛选参数并移除排序和分页参数。</summary>
    [Fact]
    public void FrozenSimpleCount_ShouldKeepFilterAndRemovePagingParameters()
    {
        const string originalSql =
            "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0 \r\nOrder By \"Name\" \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        var builder = CreateOracleBuilder().Select("Name").From("samples")
            .Where("Name", "one").OrderBy("Name").Skip(1).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        AssertOriginalPage(plan, builder, originalSql);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal("Select Count(*) \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0", count.CommandText);
        Assert.False(count.IsBuilderPlan);
        AssertCountParameters(count);
        AssertOriginalPage(plan, builder, originalSql);
    }

    /// <summary>分组冻结计划计数应保持 Oracle 子查询别名语法且移除外层分页参数。</summary>
    [Fact]
    public void FrozenGroupedCount_ShouldKeepOracleSubqueryAliasAndRemovePagingParameters()
    {
        const string originalSql =
            "Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0 \r\nGroup By \"Name\" \r\nOrder By \"Name\" \r\nOffset :p_1 Rows Fetch Next :p_2 Rows Only";
        var builder = CreateOracleBuilder().Select("Name").From("samples")
            .Where("Name", "one").GroupBy("Name").OrderBy("Name").Skip(1).Take(2);
        var plan = SqlQueryPlan.Create(builder).Freeze();

        AssertOriginalPage(plan, builder, originalSql);

        var count = SqlBuilderRuntimeBridge.CreateCountPlan(plan);

        Assert.Equal(
            "Select Count(*) \r\nFrom (Select \"Name\" \r\nFrom \"samples\" \r\nWhere \"Name\"=:p_0 \r\nGroup By \"Name\") \"t\"",
            count.CommandText);
        Assert.False(count.IsBuilderPlan);
        AssertCountParameters(count);
        AssertOriginalPage(plan, builder, originalSql);
    }

    private static OracleBuilder CreateOracleBuilder() => new(new SqlBuilderServices(options: new SqlOptions
    {
        QueryCapabilities = new SqlQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
    }));

    private static void AssertOriginalPage(SqlQueryPlan plan, ISqlBuilder builder, string expectedSql)
    {
        Assert.Equal(expectedSql, plan.CommandText);
        Assert.Equal(expectedSql, builder.ToSql());
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(new[] { ":p_0", ":p_1", ":p_2" }, parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "one", "1", "2" }, parameters.Select(parameter => parameter.Value));
    }

    private static void AssertCountParameters(SqlQueryPlan plan)
    {
        var parameters = Assert.IsType<SqlParam[]>(plan.Parameters);
        var parameter = Assert.Single(parameters);
        Assert.Equal(":p_0", parameter.Name);
        Assert.Equal("one", parameter.Value);
    }
}
