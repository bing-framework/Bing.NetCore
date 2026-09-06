using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Tests.Infrastructure;
using Bing.Tests.Models;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL 批量 Mutation 和多结果集真实执行测试。
/// </summary>
[Collection(MySqlIntegrationDatabaseCollection.Name)]
public sealed class MySqlBatchAndMultipleContractTest : IAsyncLifetime
{
    private readonly MySqlIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化 MySQL 批量和多结果集合同测试。
    /// </summary>
    /// <param name="fixture">MySQL 集成测试数据库固定装置。</param>
    public MySqlBatchAndMultipleContractTest(MySqlIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：MySQL 的实体批量 Insert、Update、逻辑 Delete 应保持影响行数和数据库状态一致。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task BatchCrud_WhenEntitiesAreProvided_ShouldPersistAndMutateRows()
    {
        // Arrange
        var entities = Enumerable.Range(1, 3).Select(index => new Product(Guid.NewGuid())
        {
            Code = $"batch-{index}",
            Name = "before"
        }).ToArray();
        using var executor = _fixture.CreateExecutor();

        // Act
        var inserted = await executor.InsertBatchAsync(entities, new SqlBatchInsertOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });
        foreach (var entity in entities)
            entity.Name = "after";
        var updated = await executor.UpdateBatchAsync(entities, new SqlBatchUpdateOptions
        {
            BatchSize = 2,
            UseTransaction = true,
            UpdateOptions = new SqlUpdateOptions
            {
                IncludeProperties = new[] { nameof(Product.Name) }
            }
        });
        var deleted = await executor.DeleteBatchAsync(entities, new SqlBatchDeleteOptions
        {
            BatchSize = 2,
            UseTransaction = true
        });

        // Assert
        Assert.Equal(3, inserted);
        Assert.Equal(3, updated);
        Assert.Equal(3, deleted);
        using var query = _fixture.CreateQuery();
        Assert.Equal(3, query.Sql("Select Count(*) From Product Where IsDeleted = 1").Scalar<int>());
    }

    /// <summary>
    /// 测试目的：MySQL 批量事务在后续批次失败时应回滚前面已经执行的批次，避免部分提交。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task InsertBatch_WhenLaterBatchFails_ShouldRollbackEarlierBatches()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entities = new[]
        {
            new Product(id) { Code = "batch-first" },
            new Product(id) { Code = "batch-duplicate" }
        };
        using var executor = _fixture.CreateExecutor();

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => executor.InsertBatchAsync(entities,
            new SqlBatchInsertOptions { BatchSize = 1, UseTransaction = true }));

        // Assert
        using var query = _fixture.CreateQuery();
        Assert.Equal(0, query.Sql("Select Count(*) From Product").Scalar<int>());
    }

    /// <summary>
    /// 测试目的：MySQL 多结果集应按 SQL 顺序读取结果，并在同步释放后允许执行器再次执行。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task Execute_WhenResultsAreReadInOrder_ShouldReleaseResourcesAfterDispose()
    {
        // Arrange
        await InsertProductAsync("multiple-first");
        await InsertProductAsync("multiple-second");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateMultipleQueryCommand(executor);

        // Act
        using (var result = executor.Execute(command))
            Assert.Equal(new[] { "multiple-first", "multiple-second" }, result.Read<CodeRow>().Select(row => row.Code));
        using var nextResult = executor.Execute(command);
        var nextRows = nextResult.Read<CodeRow>();
        var nextCount = nextResult.Read<int>();

        // Assert
        Assert.Equal(2, nextRows.Count);
        Assert.Equal(2, nextCount.Single());
    }

    /// <summary>
    /// 测试目的：MySQL 异步多结果集应保持结果顺序，异步释放后执行器应可复用。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ExecuteAsync_WhenResultsAreReadInOrder_ShouldReleaseResourcesAfterDispose()
    {
        // Arrange
        await InsertProductAsync("multiple-async");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateMultipleQueryCommand(executor);

        // Act
        await using (var result = await executor.ExecuteAsync(command))
        {
            var rows = await result.ReadAsync<CodeRow>(CancellationToken.None);
            var count = await result.ReadAsync<int>(CancellationToken.None);
            Assert.Single(rows);
            Assert.Equal("multiple-async", rows[0].Code);
            Assert.Equal(1, count.Single());
        }
        using var nextResult = await executor.ExecuteAsync(command);
        var nextRows = await nextResult.ReadAsync<CodeRow>(CancellationToken.None);
        var nextCount = await nextResult.ReadAsync<int>(CancellationToken.None);

        // Assert
        Assert.Single(nextRows);
        Assert.Equal(1, nextCount.Single());
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task InsertProductAsync(string code)
    {
        using var executor = _fixture.CreateExecutor();
        await executor.ExecuteSqlAsync("Insert Product(ProductId,Code) Values(@id,@code)",
            new { id = Guid.NewGuid(), code });
    }

    private static Bing.Data.Sql.Builders.Multiple.SqlMultipleQueryCommand CreateMultipleQueryCommand(
        ISqlMultipleQueryExecutor executor) => executor.CreateBatch()
        .Append("Select Code From Product Order By Code")
        .Append("Select Count(*) From Product")
        .Build();

    private sealed class CodeRow
    {
        public string Code { get; set; }
    }
}