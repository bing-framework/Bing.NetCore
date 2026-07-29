using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql.Tests.Builders.Mutations.Batching;

/// <summary>
/// Mutation 批次规划器测试。
/// </summary>
public sealed class SqlMutationBatchPlannerTest
{
    /// <summary>
    /// 测试目的：参数限制、SQL 长度限制和用户指定批量大小应共同确定实际分片大小。
    /// </summary>
    [Fact]
    public void Plan_WhenMultipleLimitsConfigured_ShouldUseSmallestCapacity()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 10, parametersPerEntity: 3,
            existingParameterCount: 1, maxParameterCount: 13, estimatedSqlLengthPerEntity: 12,
            maxSqlLength: 60, options: new SqlMutationBatchOptions { BatchSize = 5 });

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(4, plan.EffectiveBatchSize);
        Assert.Equal(new[] { 4, 4, 2 }, plan.BatchSizes);
    }

    /// <summary>
    /// 测试目的：空实体集合应生成空计划，供执行器直接返回零行影响数。
    /// </summary>
    [Fact]
    public void Plan_WhenEntitySetIsEmpty_ShouldReturnEmptyPlan()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 0, parametersPerEntity: 1);

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(0, plan.EffectiveBatchSize);
        Assert.Empty(plan.BatchSizes);
    }

    /// <summary>
    /// 测试目的：未配置容量限制时应把全部实体放入一个批次，且不能因无限容量发生整数溢出。
    /// </summary>
    [Fact]
    public void Plan_WhenNoCapacityLimitConfigured_ShouldCreateSingleBatch()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 2, parametersPerEntity: 1);

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(2, plan.EffectiveBatchSize);
        Assert.Equal(new[] { 2 }, plan.BatchSizes);
    }

    /// <summary>
    /// 测试目的：Provider 参数上限无法容纳一个实体时应拒绝生成不可执行计划。
    /// </summary>
    [Fact]
    public void Plan_WhenParameterLimitCannotFitOneEntity_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 1, parametersPerEntity: 2,
            existingParameterCount: 1, maxParameterCount: 2);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => planner.Plan(context));

        // Assert
        Assert.Equal("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。", exception.Message);
    }
}