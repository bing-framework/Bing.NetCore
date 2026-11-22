using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// Lambda 查询计划、别名作用域和投影形状的职责级测试。
/// </summary>
public sealed class SqlQueryPlanModelTest
{
    /// <summary>
    /// 测试目的：三种别名策略都应产生确定且互不修改的来源映射。
    /// </summary>
    [Fact]
    public void AliasScope_ShouldApplyPolicyWithoutMutatingPlan()
    {
        var source = new SqlLambdaSourceSlot(0, typeof(Customer), "customer");
        var plan = new SqlLambdaQueryPlan(new[] { source }, aliasPolicy: SqlAliasPolicy.GeneratedStable);

        var aliases = plan.CreateAliasScope(new SqlQueryScope("orders")).Aliases;

        Assert.Equal("orders_customer", aliases["source_0"]);
        Assert.Equal("orders_customer", plan.CreateAliasScope(new SqlQueryScope("orders")).Resolve("source_0"));
        Assert.Equal(SqlAliasPolicy.GeneratedStable, plan.AliasPolicy);
        Assert.Equal("customer", source.LambdaParameterName);
    }

    /// <summary>
    /// 测试目的：ExplicitOnly 不应偷偷为没有显式别名的来源生成别名。
    /// </summary>
    [Fact]
    public void AliasScope_ExplicitOnly_ShouldKeepMissingAliasEmpty()
    {
        var plan = new SqlLambdaQueryPlan(
            new[] { new SqlLambdaSourceSlot(0, typeof(Customer), "customer") },
            aliasPolicy: SqlAliasPolicy.ExplicitOnly);

        Assert.Null(plan.CreateAliasScope().Resolve("source_0"));
    }

    /// <summary>
    /// 测试目的：投影形状应保存聚合、成员和构造策略，并拒绝重复目标成员。
    /// </summary>
    [Fact]
    public void ProjectionShape_ShouldFreezeMembersAndAggregates()
    {
        var aggregate = new SqlProjectionAggregate(
            SqlAggregateFunction.Count,
            new SqlLambdaColumnReference("source_0", "Id"),
            distinct: false,
            resultAlias: "Total",
            resultType: typeof(long));
        var member = new SqlProjectionMember("Total", "Id", "source_0", "Total", typeof(long), aggregate: aggregate);

        var shape = new SqlProjectionShape(
            typeof(Summary),
            new[] { member },
            SqlProjectionConstruction.Constructor,
            new SqlProjectionSourceBinding("source_0", typeof(Customer)),
            new[] { aggregate });

        Assert.Equal(SqlProjectionConstruction.Constructor, shape.Construction);
        Assert.Same(aggregate, Assert.Single(shape.Aggregates));
        Assert.Equal("Total", Assert.Single(shape.Members).ResultAlias);
        Assert.Throws<ArgumentException>(() => new SqlProjectionShape(
            typeof(Summary),
            new[] { member, new SqlProjectionMember("total", "Name", "source_0", "total", typeof(string)) },
            SqlProjectionConstruction.Constructor,
            new SqlProjectionSourceBinding("source_0")));
    }

    private sealed class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Summary
    {
        public long Total { get; set; }
    }
}
