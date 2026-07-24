using Bing.Dapper.Tests.Infrastructure;
using Bing.Test.Shared;

namespace Bing.Dapper.Tests.SqlExecutor;

/// <summary>
/// PostgreSQL SQL 执行器真实执行集成测试。
/// </summary>
[Collection(PostgreSqlIntegrationDatabaseCollection.Name)]
public sealed class PostgreSqlExecutorTest : IAsyncLifetime
{
    /// <summary>
    /// PostgreSQL 集成测试数据库固定装置。
    /// </summary>
    private readonly PostgreSqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="PostgreSqlExecutorTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">PostgreSQL 集成测试数据库固定装置。</param>
    public PostgreSqlExecutorTest(PostgreSqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 测试 - PostgreSQL 执行器应返回 Insert、Update、Delete 与无匹配更新的实际影响行数。
    /// </summary>
    [IntegrationFact("PostgreSql")]
    public async Task ExecuteSqlAsync_ShouldReturnAffectedRowsForInsertUpdateAndDelete()
    {
        // Arrange
        var id = Guid.NewGuid();
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.ExecuteSqlAsync(
            "Insert Into public.integration_products(id,code,name,amount,occurred_at) Values(@id,@code,@name,@amount,@occurredAt)",
            new { id, code = "executor", name = "before", amount = 1m, occurredAt = new DateTime(2026, 7, 24) });
        var updated = await executor.ExecuteSqlAsync("Update public.integration_products Set name=@name Where id=@id",
            new { id, name = "after" });
        var deleted = await executor.ExecuteSqlAsync("Delete From public.integration_products Where id=@id", new { id });
        var unmatched = await executor.ExecuteSqlAsync("Update public.integration_products Set name=@name Where id=@id",
            new { id = Guid.NewGuid(), name = "missing" });

        // Assert
        Assert.Equal(1, inserted);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
        Assert.Equal(0, unmatched);
    }
}