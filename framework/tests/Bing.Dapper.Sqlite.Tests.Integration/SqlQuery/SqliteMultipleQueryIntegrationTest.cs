using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Multiple;
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
        var rows = await result.ReadAsync<SampleName>(CancellationToken.None);
        var count = (await result.ReadAsync<int>(CancellationToken.None)).Single();

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
            await result.ReadAsync<SampleName>(CancellationToken.None);
            await result.ReadAsync<int>(CancellationToken.None);

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

    /// <summary>
    /// 测试目的：多结果集读取在开始前收到取消令牌时应释放执行租约，以便执行器继续执行后续操作。
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenCancellationRequested_ShouldReleaseExecutionResources()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = executor.CreateBatch()
            .Append("Select Name From samples")
            .Append("Select Count(*) From samples")
            .Build();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        using (var result = await executor.ExecuteAsync(command))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                result.ReadAsync<SampleName>(cancellationTokenSource.Token));
        }
        using var nextResult = await executor.ExecuteAsync(command);
        await nextResult.ReadAsync<SampleName>(CancellationToken.None);
        var count = await nextResult.ReadAsync<int>(CancellationToken.None);

        // Assert
        Assert.Single(count);
        Assert.Equal(1, count[0]);
    }

    /// <summary>
    /// 测试目的：异步创建的多结果集使用同步 Dispose 时不得同步等待异步完成回调，且释放后执行器应可复用。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenResultIsDisposedSynchronously_ShouldReleaseExecutionResources()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateTwoResultCommand(executor);

        // Act
        var result = await executor.ExecuteAsync(command);
        result.Dispose();

        // Assert
        using var nextResult = executor.Execute(command);
        Assert.Single(nextResult.Read<SampleName>());
        Assert.Single(nextResult.Read<int>());
    }

    /// <summary>
    /// 测试目的：同步创建的多结果集使用异步 Dispose 时应完成异步事务和租约清理，且释放后执行器应可复用。
    /// </summary>
    [Fact]
    public async Task Execute_WhenResultIsDisposedAsynchronously_ShouldReleaseExecutionResources()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateTwoResultCommand(executor);

        // Act
        var result = executor.Execute(command);
        await result.DisposeAsync();

        // Assert
        using var nextResult = await executor.ExecuteAsync(command);
        Assert.Single(await nextResult.ReadAsync<SampleName>(CancellationToken.None));
        Assert.Single(await nextResult.ReadAsync<int>(CancellationToken.None));
    }

    /// <summary>
    /// 测试目的：结果读取映射失败时应自动关闭读取器并归还执行租约，使同一执行器可以再次执行。
    /// </summary>
    [Fact]
    public async Task Read_WhenMaterializationFails_ShouldReleaseExecutionResources()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = executor.CreateBatch()
            .Append("Select Name From samples")
            .Append("Select Count(*) From samples")
            .Build();
        using (var result = executor.Execute(command))
        {
            // Act
            Assert.ThrowsAny<Exception>(() => result.Read<int>());
        }

        // Assert
        using var nextResult = executor.Execute(command);
        Assert.Single(nextResult.Read<SampleName>());
        Assert.Single(nextResult.Read<int>());
    }

    /// <summary>
    /// 测试目的：同步和异步重复释放都应保持幂等，不得重复完成事务或归还租约。
    /// </summary>
    [Fact]
    public async Task Dispose_WhenCalledRepeatedly_ShouldRemainIdempotent()
    {
        // Arrange
        await _fixture.InsertSampleAsync("first", 1m, "secret-1");
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateTwoResultCommand(executor);
        var result = await executor.ExecuteAsync(command);

        // Act
        result.Dispose();
        result.Dispose();
        await result.DisposeAsync();

        // Assert
        using var nextResult = executor.Execute(command);
        Assert.Single(nextResult.Read<SampleName>());
        Assert.Single(nextResult.Read<int>());
    }

    /// <inheritdoc />
    public Task InitializeAsync() => _fixture.ResetAsync();

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 创建固定的两个结果集命令。
    /// </summary>
    private static SqlMultipleQueryCommand CreateTwoResultCommand(ISqlMultipleQueryExecutor executor) => executor.CreateBatch()
        .Append("Select Name From samples")
        .Append("Select Count(*) From samples")
        .Build();

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