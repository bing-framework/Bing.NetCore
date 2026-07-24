using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询聚合真实执行集成测试。
/// </summary>
public partial class MySqlQueryTest
{
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
        query.Count("p.ProductId", "Count").From("Product", "p");

        // Act
        var result = query.ExecuteScalar<int>();

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
        query.Sum("p.Price", "Total").From("Product", "p");

        // Act
        var result = query.ExecuteScalar<decimal>();

        // Assert
        Assert.Equal(20m, result);
    }
}