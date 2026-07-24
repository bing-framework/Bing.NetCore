using Bing.Data.Sql;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL SQL 查询聚合真实执行集成测试。
/// </summary>
public sealed partial class PostgreSqlQueryTest
{
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
        query.Count("p.id", "Count").From("public.integration_products", "p");

        // Act
        var result = query.ExecuteScalar<int>();

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
        query.Sum("p.amount", "Total").From("public.integration_products", "p");

        // Act
        var result = query.ExecuteScalar<decimal>();

        // Assert
        Assert.Equal(20m, result);
    }
}