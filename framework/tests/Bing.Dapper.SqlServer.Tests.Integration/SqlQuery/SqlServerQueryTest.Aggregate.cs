using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQL Server 聚合真实执行集成测试。
/// </summary>
[Collection(SqlServerIntegrationDatabaseCollection.Name)]
public sealed class SqlServerQueryAggregateTest : IAsyncLifetime
{
    /// <summary>
    /// SQL Server 集成测试数据库固定装置。
    /// </summary>
    private readonly SqlServerIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化 SQL Server 聚合集成测试。
    /// </summary>
    /// <param name="fixture">SQL Server 集成测试数据库固定装置。</param>
    public SqlServerQueryAggregateTest(SqlServerIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 每个测试前清空受控测试表。
    /// </summary>
    /// <returns>异步任务。</returns>
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <summary>
    /// 测试类结束时无需额外清理。
    /// </summary>
    /// <returns>已完成任务。</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 测试 - SQL Server 应真实执行统一聚合、条件表达式、Raw 参数和显式参数绑定。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task AggregateApis_WhenAggregateDataIsSeeded_ShouldExecuteExpectedValues()
    {
        // Arrange
        await _fixture.SeedAggregateDataAsync();

        // Act
        using var countAllQuery = CreateAggregateQuery();
        var countAll = await countAllQuery.Count().ExecuteScalarAsync<int>();
        using var countColumnQuery = CreateAggregateQuery();
        var distinctUsers = await countColumnQuery.Count("p.UserId", distinct: true).ExecuteScalarAsync<int>();
        using var sumQuery = CreateAggregateQuery();
        var total = await sumQuery.Sum("p.Amount").ExecuteScalarAsync<decimal>();
        using var expressionQuery = CreateAggregateQuery();
        var conditionalTotal = await expressionQuery.AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [p].[Amount]>@MinAmount Then [p].[Amount] Else 0 End")
            .AddParam("MinAmount", 15)
            .ExecuteScalarAsync<decimal>();
        using var rawQuery = CreateAggregateQuery();
        var enabledTotal = await rawQuery.AggregateRaw(SqlAggregateFunction.Sum,
                "Case When p.Enabled=1 Then p.Amount Else 0 End")
            .ExecuteScalarAsync<decimal>();

        // Assert
        Assert.Equal(4, countAll);
        Assert.Equal(2, distinctUsers);
        Assert.Equal(40m, total);
        Assert.Equal(20m, conditionalTotal);
        Assert.Equal(20m, enabledTotal);
    }

    /// <summary>
    /// 创建指向受控聚合集成测试表的查询。
    /// </summary>
    /// <returns>SQL Server 查询对象。</returns>
    private ISqlQuery CreateAggregateQuery() => _fixture.CreateQuery().From("dbo.BingSqlAggregateIntegration", "p");
}