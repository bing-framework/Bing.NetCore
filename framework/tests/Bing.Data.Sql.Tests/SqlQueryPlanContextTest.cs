using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 查询计划上下文和执行快照测试。
/// </summary>
public class SqlQueryPlanContextTest
{
    /// <summary>
    /// 测试目的：计划开始执行时应生成执行标识，并保留查询上下文和阶段信息。
    /// </summary>
    [Fact]
    public void NotifyExecutionStarted_ShouldCreateExecutionContext()
    {
        // Arrange
        var plan = SqlQueryPlan.Create(new TestSqlBuilder());
        plan.SetContext("query-1", "parent-1", "Data");

        // Act
        plan.NotifyExecutionStarted();

        // Assert
        Assert.Equal("query-1", plan.QueryContextId);
        Assert.Equal("parent-1", plan.ParentQueryContextId);
        Assert.Equal("Data", plan.Phase);
        Assert.False(string.IsNullOrWhiteSpace(plan.ExecutionId));
    }

    /// <summary>
    /// 测试目的：Count 派生计划应共享来源查询上下文，但使用独立阶段和 Builder。
    /// </summary>
    [Fact]
    public void DerivedPlan_ShouldCopyContextWithoutSharingBuilder()
    {
        // Arrange
        var sourceBuilder = new TestSqlBuilder().Select("Id").From("Users");
        var source = SqlQueryPlan.Create(sourceBuilder);
        source.SetContext("query-1", null, "Data");
        var derived = SqlQueryPlan.Create(sourceBuilder.Clone());

        // Act
        derived.CopyContextFrom(source, "Count");
        derived.GetBuilder().Where("Id", 1);

        // Assert
        Assert.Equal(source.QueryContextId, derived.QueryContextId);
        Assert.Equal("Count", derived.Phase);
        Assert.NotSame(source.GetBuilder(), derived.GetBuilder());
        Assert.DoesNotContain("Where", source.GetBuilder().ToSql());
    }

    /// <summary>
    /// 测试目的：Count/Data 派生计划应共享查询上下文但分别生成执行标识和阶段，不能复用同一执行状态。
    /// </summary>
    [Fact]
    public void DerivedCountAndDataPlans_WhenExecuted_ShouldUseIndependentExecutionContexts()
    {
        // Arrange
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Id").From("Users"));
        source.SetContext("query-1", "parent-1", "Data");
        var count = SqlQueryPlan.Create(source.GetBuilder().Clone());
        var data = SqlQueryPlan.Create(source.GetBuilder().Clone());
        count.CopyContextFrom(source, "Count");
        data.CopyContextFrom(source, "Data");

        // Act
        count.NotifyExecutionStarted();
        data.NotifyExecutionStarted();

        // Assert
        Assert.Equal(source.QueryContextId, count.QueryContextId);
        Assert.Equal(source.QueryContextId, data.QueryContextId);
        Assert.Equal("Count", count.Phase);
        Assert.Equal("Data", data.Phase);
        Assert.NotEqual(count.ExecutionId, data.ExecutionId);
        Assert.NotSame(count.GetBuilder(), data.GetBuilder());
    }

    /// <summary>
    /// 测试目的：执行快照应深复制数组参数，调用方后续修改原数组不得污染快照。
    /// </summary>
    [Fact]
    public void ExecutionSnapshot_WhenSourceArrayChanges_ShouldRemainIndependent()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var parameter = new SqlParam("@values", values);

        // Act
        var snapshot = new SqlBuilderExecutionSnapshot("Select 1", new[] { parameter });
        values[0] = 99;

        // Assert
        var snapshotValues = Assert.IsType<int[]>(Assert.Single(snapshot.Parameters).Value);
        Assert.Equal(new[] { 1, 2 }, snapshotValues);
        Assert.NotSame(values, snapshotValues);
    }
}