using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQLite 多结果集查询集成测试。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteMultipleQueryIntegrationTest : IAsyncLifetime
{
    /// <summary>
    /// SQLite 集成测试数据库固定装置。
    /// </summary>
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个<see cref="SqliteMultipleQueryIntegrationTest"/>类型的实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成测试数据库固定装置。</param>
    public SqliteMultipleQueryIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：SQLite 应在一次命令中按顺序读取多个结果集，并保留 SqlParam 参数绑定。
    /// </summary>
    [Fact]
    public async Task Execute_WhenBatchContainsTwoQueries_ShouldReadResultsInOrder()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        await _fixture.InsertSampleAsync("second", 2m, "secret-2");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = executor.CreateBatch()
            .Append("Select Id, Name From samples Where Name = @name", new[] { new SqlParam("name", "first") })
            .Append("Select Count(*) From samples")
            .Build();

        // Act
        using var result = await executor.ExecuteAsync(command);
        var rows = await result.ReadAsync<SampleName>();
        var count = (await result.ReadAsync<int>()).Single();

        // Assert
        Assert.Single(rows);
        Assert.Equal("first", rows[0].Name);
        Assert.Equal(2, count);
    }

    /// <summary>
    /// 测试目的：结果读取器存活期间，同一个执行器不得启动第二个操作；释放后应恢复可用。
    /// </summary>
    [Fact]
    public async Task Execute_WhenResultIsActive_ShouldRejectReentrantExecutionUntilDisposed()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = executor.CreateBatch()
            .Append("Select Name From samples")
            .Append("Select Count(*) From samples")
            .Build();

        // Act
        using (var result = executor.Execute(command))
        {
            var exception = Assert.Throws<InvalidOperationException>(() => executor.Execute(command));
            await result.ReadAsync<SampleName>();
            await result.ReadAsync<int>();

            // Assert
            Assert.Equal("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。", exception.Message);
        }
        using var nextResult = executor.Execute(command);
        nextResult.Read<SampleName>();
        var count = nextResult.Read<int>();

        // Assert
        Assert.Single(count);
        Assert.Equal(1, count[0]);
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 多结果集样例名称投影。
    /// </summary>
    private sealed class SampleName
    {
        /// <summary>
        /// 样例名称。
        /// </summary>
        public string Name { get; set; }
    }
}