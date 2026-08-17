using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
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
        using var query = _fixture.CreateQuery();
        var countAll = await CreateAggregateDescription<int>(query).CountAll().ScalarAsync();
        var distinctUsers = await CreateAggregateDescription<int>(query).CountColumn("p.UserId", distinct: true).ScalarAsync();
        var total = await CreateAggregateDescription<decimal>(query).Sum("p.Amount").ScalarAsync();
        var conditionalTotal = await CreateAggregateDescription<decimal>(query).AggregateExpression(SqlAggregateFunction.Sum,
                "Case When [p].[Amount]>@MinAmount Then [p].[Amount] Else 0 End")
            .AddParam("MinAmount", 15)
            .ScalarAsync();
        var enabledTotal = await CreateAggregateDescription<decimal>(query).AggregateRaw(SqlAggregateFunction.Sum,
                "Case When p.Enabled=1 Then p.Amount Else 0 End")
            .ScalarAsync();

        // Assert
        Assert.Equal(4, countAll);
        Assert.Equal(2, distinctUsers);
        Assert.Equal(40m, total);
        Assert.Equal(20m, conditionalTotal);
        Assert.Equal(20m, enabledTotal);
    }

    /// <summary>
    /// 测试目的：SQL Server 多行 Insert Output 应通过查询结果 API 物化全部 INSERTED 行。
    /// </summary>
    [IntegrationFact("SqlServer")]
    [Trait("Category", "Integration")]
    [Trait("Database", "SqlServer")]
    public async Task ExecuteQueryAsync_WhenInsertOutputIsConfigured_ShouldMaterializeReturnedRows()
    {
        // Arrange
        using var executor = _fixture.CreateExecutor();
        IEnumerable<IReadOnlyList<object>> values = new IReadOnlyList<object>[]
        {
            new object[] { "output-first", 1m, true },
            new object[] { "output-second", 2m, false }
        };
        var builder = executor.CreateWriteBuilder()
            .InsertInto(new SqlTableReference { Schema = "dbo", TableName = "BingSqlAggregateIntegration" })
            .Columns("UserId", "Amount", "Enabled")
            .Values(values)
            .Returning<SqlServerOutputRow>(row => new { row.Id, row.UserId });

        // Act
        var rows = await executor.ExecuteReturningAsync<SqlServerOutputRow>(builder.ToSqlWriteCommand());

        // Assert
        Assert.Equal(new[] { "output-first", "output-second" }, rows.Select(row => row.UserId));
        Assert.All(rows, row => Assert.True(row.Id > 0));
    }

    /// <summary>
    /// 创建指向受控聚合集成测试表的独立查询描述。
    /// </summary>
    /// <typeparam name="TResult">聚合结果映射类型。</typeparam>
    /// <param name="query">承载连接和事务资源的根查询。</param>
    /// <returns>SQL Server 独立查询描述。</returns>
    private static SqlQuery<TResult> CreateAggregateDescription<TResult>(ISqlQuery query) =>
        query.Query<TResult>().From("dbo.BingSqlAggregateIntegration", "p");

    /// <summary>
    /// SQL Server Output 物化模型。
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.Table("BingSqlAggregateIntegration", Schema = "dbo")]
    private sealed class SqlServerOutputRow
    {
        /// <summary>标识。</summary>
        public int Id { get; set; }

        /// <summary>用户标识。</summary>
        public string UserId { get; set; }
    }
}