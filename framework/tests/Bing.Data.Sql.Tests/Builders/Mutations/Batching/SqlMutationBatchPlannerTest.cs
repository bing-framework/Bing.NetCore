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

    /// <summary>
    /// 测试目的：既有参数必须占用 Provider 总参数上限，规划器只能使用剩余容量确定批次大小。
    /// </summary>
    [Fact]
    public void Plan_WhenExistingParametersConsumeCapacity_ShouldUseRemainingParameterSlots()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 5, parametersPerEntity: 3,
            existingParameterCount: 1, maxParameterCount: 10);

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(3, plan.EffectiveBatchSize);
        Assert.Equal(new[] { 3, 2 }, plan.BatchSizes);
    }

    /// <summary>
    /// 测试目的：SQL 长度限制应在恰好容纳整数实体数量时稳定分片，最后一批只包含余数实体。
    /// </summary>
    [Fact]
    public void Plan_WhenSqlLengthLimitIsConfigured_ShouldUseExactSqlLengthCapacity()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 5, parametersPerEntity: 1,
            estimatedSqlLengthPerEntity: 12, maxSqlLength: 24);

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(2, plan.EffectiveBatchSize);
        Assert.Equal(new[] { 2, 2, 1 }, plan.BatchSizes);
    }

    /// <summary>
    /// 测试目的：用户指定零或负数批量大小时，规划器应在生成计划前拒绝无效配置。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Plan_WhenUserBatchSizeIsNotPositive_ShouldThrowArgumentOutOfRangeException(int batchSize)
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: 1, parametersPerEntity: 1,
            options: new SqlMutationBatchOptions { BatchSize = batchSize });

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(context));

        // Assert
        Assert.Equal("context", exception.ParamName);
        Assert.Contains("批量大小必须大于零。", exception.Message);
    }

    /// <summary>
    /// 测试目的：极大实体集合按单条分片时，计划应按需计算批次大小而非预分配与实体数量相同的列表。
    /// </summary>
    [Fact]
    public void Plan_WhenEntityCountIsIntMaxAndBatchSizeIsOne_ShouldUseLazyBatchSizes()
    {
        // Arrange
        var planner = new SqlMutationBatchPlanner();
        var context = new SqlMutationBatchPlanContext(entityCount: int.MaxValue, parametersPerEntity: 1,
            options: new SqlMutationBatchOptions { BatchSize = 1 });

        // Act
        var plan = planner.Plan(context);

        // Assert
        Assert.Equal(int.MaxValue, plan.BatchSizes.Count);
        Assert.Equal(1, plan.BatchSizes[0]);
        Assert.Equal(1, plan.BatchSizes[plan.BatchSizes.Count - 1]);
        Assert.False(plan.BatchSizes is List<int>);
    }
}