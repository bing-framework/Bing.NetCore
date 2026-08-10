using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询聚合真实执行集成测试。
/// </summary>
public partial class MySqlQueryTest
{
    /// <summary>
    /// 测试 - MySQL Count 应忽略 null 聚合参数并返回非空行数。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task Count_WhenAggregateDataContainsNull_ShouldReturnNonNullCount()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.Price", "Count");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(3, result);
    }

    /// <summary>
    /// 测试 - MySQL Count Distinct 应去除重复金额并忽略 null。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task CountDistinct_WhenAggregateDataContainsDuplicates_ShouldReturnDistinctCount()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.Price", "Count", distinct: true);

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - MySQL Sum、Sum Distinct、Avg 与 Avg Distinct 应返回数据库聚合结果。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task SumAndAvg_WhenAggregateDataContainsDuplicates_ShouldReturnExpectedValues()
    {
        // Arrange
        await SeedAggregateDataAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var sum = CreateAggregateDescription<decimal>(query).Sum("p.Price", "Total").Scalar();
        var distinctSum = CreateAggregateDescription<decimal>(query).Sum("p.Price", "Total", distinct: true).Scalar();
        var average = CreateAggregateDescription<decimal>(query).Avg("p.Price", "Average").Scalar();
        var distinctAverage = CreateAggregateDescription<decimal>(query).Avg("p.Price", "Average", distinct: true)
            .Scalar();

        // Assert
        Assert.Equal(40m, sum);
        Assert.Equal(30m, distinctSum);
        Assert.Equal(13.333333m, average);
        Assert.Equal(15m, distinctAverage);
    }

    /// <summary>
    /// 测试 - MySQL Max 与 Min 的 Distinct 参数应能真实执行且返回边界值。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MaxAndMin_WhenDistinctIsConfigured_ShouldReturnExtremes()
    {
        // Arrange
        await SeedAggregateDataAsync();

        // Act
        using var query = _fixture.CreateQuery();
        var maximum = CreateAggregateDescription<decimal>(query).Max("p.Price", "Maximum", distinct: true).Scalar();
        var minimum = CreateAggregateDescription<decimal>(query).Min("p.Price", "Minimum", distinct: true).Scalar();

        // Assert
        Assert.Equal(20m, maximum);
        Assert.Equal(10m, minimum);
    }

    /// <summary>
    /// 测试 - MySQL 限定列 Distinct 聚合应使用正确 SQL 并真实执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task QualifiedDistinctAggregate_WhenUserIdsRepeat_ShouldExecuteSuccessfully()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<int>(query).CountColumn("p.UserId", "UserCount", distinct: true);

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - MySQL Raw 聚合表达式应保持表达式语义并真实执行。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task AggregateExpression_WhenCaseAndArithmeticAreConfigured_ShouldExecuteSuccessfully()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var sumQuery = CreateAggregateDescription<decimal>(query)
            .AggregateExpression(SqlAggregateFunction.Sum, "[p].[Price] * 2", "DoubleTotal");
        var countQuery = CreateAggregateDescription<int>(query).AggregateExpression(SqlAggregateFunction.Count,
            "Case When [p].[Enabled]=1 Then [p].[UserId] End", "EnabledUsers", distinct: true);

        // Act
        var sum = sumQuery.Scalar();
        var count = countQuery.Scalar();

        // Assert
        Assert.Equal(80m, sum);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// 测试 - MySQL 聚合结果别名应映射到 DTO 属性。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task Aggregate_WhenAliasesAreConfigured_ShouldMapToDto()
    {
        // Arrange
        await SeedAggregateDataAsync();
        using var query = _fixture.CreateQuery();
        var description = CreateAggregateDescription<MySqlAggregateResult>(query)
            .CountColumn("p.UserId", "UserCount", distinct: true)
            .Sum("p.Price", "DistinctAmount", distinct: true);

        // Act
        var result = description.FirstOrDefault();

        // Assert
        Assert.Equal(2, result.UserCount);
        Assert.Equal(30m, result.DistinctAmount);
    }

    /// <summary>
    /// 测试 - MySQL 限定列 Count 应真实执行成功。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MySql_CountQualifiedColumn_ShouldExecuteSuccessfully()
    {
        // Arrange
        await InitProductDataAsync(Guid.NewGuid(), "count-qualified-first");
        await InitProductDataAsync(Guid.NewGuid(), "count-qualified-second");
        using var query = _fixture.CreateQuery();
        var description = query.Sql<int>().CountColumn("p.ProductId", "Count").From("Product", "p");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(2, result);
    }

    /// <summary>
    /// 测试 - MySQL 限定列 Sum 应真实执行成功。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MySql_SumQualifiedColumn_ShouldExecuteSuccessfully()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await _sqlExecutor.ExecuteSqlAsync("Insert Product(ProductId,Code,Price) Values(@productId,@code,@price)",
            new { productId = firstId, code = "sum-qualified-first", price = 12.5m });
        await _sqlExecutor.ExecuteSqlAsync("Insert Product(ProductId,Code,Price) Values(@productId,@code,@price)",
            new { productId = secondId, code = "sum-qualified-second", price = 7.5m });
        using var query = _fixture.CreateQuery();
        var description = query.Sql<decimal>().Sum("p.Price", "Total").From("Product", "p");

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
    /// <returns>包含 Product 表别名的独立查询描述。</returns>
    private static SqlQuery<TResult> CreateAggregateDescription<TResult>(ISqlQuery query) =>
        query.Sql<TResult>().From("Product", "p");

    /// <summary>
    /// 写入包含重复值与 null 的聚合测试数据。
    /// </summary>
    /// <returns>异步写入任务。</returns>
    private async Task SeedAggregateDataAsync()
    {
        await InsertAggregateProductAsync("aggregate-1", "A", 10m);
        await InsertAggregateProductAsync("aggregate-2", "A", 10m);
        await InsertAggregateProductAsync("aggregate-3", "B", 20m);
        await InsertAggregateProductAsync("aggregate-4", null, null);
    }

    /// <summary>
    /// 写入一条聚合测试产品记录。
    /// </summary>
    /// <param name="code">产品编码。</param>
    /// <param name="userId">用户标识。</param>
    /// <param name="price">产品金额。</param>
    /// <returns>异步写入任务。</returns>
    private Task InsertAggregateProductAsync(string code, string userId, decimal? price) => _sqlExecutor.ExecuteSqlAsync(
        "Insert Product(ProductId,Code,UserId,Price) Values(@productId,@code,@userId,@price)",
        new { productId = Guid.NewGuid(), code, userId, price });
}

/// <summary>
/// MySQL 聚合结果映射模型。
/// </summary>
public sealed class MySqlAggregateResult
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