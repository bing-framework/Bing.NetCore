using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Clauses;

/// <summary>
/// 验证公开构造器不会保留调用方列表的可变所有权。
/// </summary>
public sealed class ClauseConstructorListOwnershipTest
{
    /// <summary>
    /// 构造完成后修改调用方分组列表，不得改变已创建子句的 SQL。
    /// </summary>
    [Fact]
    public void GroupByClause_WhenCallerListChanges_ShouldKeepConstructedSql()
    {
        var group = new List<SqlItem> { SqlItem.Parse("a") };
        var clause = new GroupByClause(TestSqlBuilder.CreateTestClauseContext(), group);

        group.Add(SqlItem.Parse("b"));

        Assert.Equal("Group By [a]", clause.ToSql());
    }

    /// <summary>
    /// 构造完成后修改调用方排序列表，不得改变已创建子句的 SQL。
    /// </summary>
    [Fact]
    public void OrderByClause_WhenCallerListChanges_ShouldKeepConstructedSql()
    {
        var items = new List<OrderByItem> { new("a") };
        var clause = new OrderByClause(TestSqlBuilder.CreateTestClauseContext(), items);

        items.Add(new OrderByItem("b"));

        Assert.Equal("Order By [a]", clause.ToSql());
    }
}
