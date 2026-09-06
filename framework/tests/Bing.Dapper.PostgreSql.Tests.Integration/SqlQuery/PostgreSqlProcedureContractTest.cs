using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// PostgreSQL 原生 Function/Procedure 语义集成测试。
/// </summary>
[Collection(PostgreSqlIntegrationDatabaseCollection.Name)]
public sealed class PostgreSqlProcedureContractTest : IAsyncLifetime
{
    private readonly PostgreSqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化 PostgreSQL 过程合同测试。
    /// </summary>
    /// <param name="fixture">PostgreSQL 集成测试数据库固定装置。</param>
    public PostgreSqlProcedureContractTest(PostgreSqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：PostgreSQL Function 应按原生 SELECT Function 语义物化返回表，不伪造 OUT 参数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteFunctionAsync_WhenFunctionReturnsTable_ShouldMaterializeNativeRows()
    {
        // Arrange
        using var query = _fixture.CreateQuery();

        // Act
        var rows = await query.Sql(
                "Select output_value As OutputValue, doubled_value As DoubledValue " +
                "From public.bing_sql_contract_function(@input_value)",
                new { input_value = 3 })
            .ToListAsync<FunctionResult>();
        var result = Assert.Single(rows);

        // Assert
        Assert.Equal(4, result.OutputValue);
        Assert.Equal(6, result.DoubledValue);
    }

    /// <summary>
    /// 测试目的：PostgreSQL Function 异步入口在预取消时应先传播取消，不打开或执行后续命令。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteFunctionAsync_WhenCancellationIsRequested_ShouldCancelBeforeFunctionExecution()
    {
        // Arrange
        using var query = _fixture.CreateQuery();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query
            .Sql("Select output_value From public.bing_sql_contract_function(@input_value)",
                new { input_value = 3 })
            .ToListAsync<FunctionResult>(cancellationToken: cancellationTokenSource.Token));
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class FunctionResult
    {
        public int OutputValue { get; set; }

        public int DoubledValue { get; set; }
    }
}