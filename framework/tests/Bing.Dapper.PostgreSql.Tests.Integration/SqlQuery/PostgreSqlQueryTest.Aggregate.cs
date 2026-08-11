using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL SQL 查询聚合真实执行集成测试。
/// </summary>
public sealed partial class PostgreSqlQueryTest
{
    /// <summary>
    /// 测试 - PostgreSQL Count 应忽略 null 聚合参数并返回非空行数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task Count_WhenAggregateDataContainsNull_ShouldReturnNonNullCount()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.amount", "Count");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(3, result);
    }

    /// <summary>
    /// 测试 - PostgreSQL Count Distinct 应去除重复金额并忽略 null。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task CountDistinct_WhenAggregateDataContainsDuplicates_ShouldReturnDistinctCount()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.amount", "Count", distinct: true);

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - PostgreSQL Sum、Sum Distinct、Avg 与 Avg Distinct 应返回数据库聚合结果。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task SumAndAvg_WhenAggregateDataContainsDuplicates_ShouldReturnExpectedValues()
    {
        // Arrange
        await SeedAggregateDataAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var sum = CreateAggregateDescription<decimal>(query).Sum("p.amount", "Total").Scalar();
        var distinctSum = CreateAggregateDescription<decimal>(query).Sum("p.amount", "Total", distinct: true).Scalar();
        var average = CreateAggregateDescription<decimal>(query).Avg("p.amount", "Average").Scalar();
        var distinctAverage = CreateAggregateDescription<decimal>(query).Avg("p.amount", "Average", distinct: true)
            .Scalar();

        // Assert
        Assert.Equal(40m, sum);
        Assert.Equal(30m, distinctSum);
        Assert.Equal(13.3333333333333333m, average);
        Assert.Equal(15m, distinctAverage);
    }

    /// <summary>
    /// 测试 - PostgreSQL Max 与 Min 的 Distinct 参数应能真实执行且返回边界值。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task MaxAndMin_WhenDistinctIsConfigured_ShouldReturnExtremes()
    {
        // Arrange
        await SeedAggregateDataAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var maximum = CreateAggregateDescription<decimal>(query).Max("p.amount", "Maximum", distinct: true).Scalar();
        var minimum = CreateAggregateDescription<decimal>(query).Min("p.amount", "Minimum", distinct: true).Scalar();

        // Assert
        Assert.Equal(20m, maximum);
        Assert.Equal(10m, minimum);
    }

    /// <summary>
    /// 测试 - PostgreSQL 限定列 Distinct 聚合应使用正确 SQL 并真实执行。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task QualifiedDistinctAggregate_WhenUserIdsRepeat_ShouldExecuteSuccessfully()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.user_id", "UserCount", distinct: true);

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - PostgreSQL Raw 聚合表达式应保持表达式语义并真实执行。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task AggregateExpression_WhenCaseAndArithmeticAreConfigured_ShouldExecuteSuccessfully()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var sumQuery = CreateAggregateDescription<decimal>(query)
            .AggregateExpression(SqlAggregateFunction.Sum, "[p].[amount] * 2", "DoubleTotal");
        var countQuery = CreateAggregateDescription<int>(query).AggregateExpression(SqlAggregateFunction.Count,
            "Case When [p].[amount] Is Not Null Then [p].[user_id] End", "EnabledUsers", distinct: true);

        // Act
        var sum = sumQuery.Scalar();
        var count = countQuery.Scalar();

        // Assert
        Assert.Equal(80m, sum);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// 测试 - PostgreSQL 聚合结果别名应映射到 DTO 属性。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task Aggregate_WhenAliasesAreConfigured_ShouldMapToDto()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<PostgreSqlAggregateResult>(query)
            .CountColumn("p.user_id", "UserCount", distinct: true)
            .Sum("p.amount", "DistinctAmount", distinct: true);

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal(2, result.UserCount);
        Assert.Equal(30m, result.DistinctAmount);
    }

    /// <summary>
    /// 测试 - PostgreSQL 限定列 Count 应真实执行成功。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task PostgreSql_CountQualifiedColumn_ShouldExecuteSuccessfully()
    {
        // Arrange
        await InsertProductAsync(Guid.NewGuid(), "count-qualified-first");
        await InsertProductAsync(Guid.NewGuid(), "count-qualified-second");
        using var query = _fixture.CreateQuery();
        var description = query.Query<int>().CountColumn("p.id", "Count").From("public.integration_products", "p");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - PostgreSQL 限定列 Sum 应真实执行成功。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task PostgreSql_SumQualifiedColumn_ShouldExecuteSuccessfully()
    {
        // Arrange
        await InsertProductAsync(Guid.NewGuid(), "sum-qualified-first", amount: 12.5m);
        await InsertProductAsync(Guid.NewGuid(), "sum-qualified-second", amount: 7.5m);
        using var query = _fixture.CreateQuery();
        var description = query.Query<decimal>().Sum("p.amount", "Total").From("public.integration_products", "p");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(20m, result);
    }

    /// <summary>
    /// 创建聚合测试独立查询描述。
    /// </summary>
    /// <typeparam name="TResult">聚合结果映射类型。</typeparam>
    /// <param name="query">承载连接和事务资源的根查询。</param>
    /// <returns>包含产品表别名的独立查询描述。</returns>
    private static SqlQuery<TResult> CreateAggregateDescription<TResult>(ISqlQuery query) =>
        query.Query<TResult>().From("public.integration_products", "p");

    /// <summary>
    /// 写入包含重复值与 null 的聚合测试数据。
    /// </summary>
    /// <returns>异步写入任务。</returns>
    private async Task SeedAggregateDataAsync()
    {
        await InsertProductAsync(Guid.NewGuid(), "aggregate-1", amount: 10m, userId: "A");
        await InsertProductAsync(Guid.NewGuid(), "aggregate-2", amount: 10m, userId: "A");
        await InsertProductAsync(Guid.NewGuid(), "aggregate-3", amount: 20m, userId: "B");
        await InsertProductAsync(Guid.NewGuid(), "aggregate-4", amount: null, userId: null);
    }
}

/// <summary>
/// PostgreSQL 聚合结果映射模型。
/// </summary>
public sealed class PostgreSqlAggregateResult
{
    /// <summary>
    /// 去重后的用户数量。
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    /// 去重后的金额合计。
    /// </summary>
    public decimal DistinctAmount { get; set; }
}