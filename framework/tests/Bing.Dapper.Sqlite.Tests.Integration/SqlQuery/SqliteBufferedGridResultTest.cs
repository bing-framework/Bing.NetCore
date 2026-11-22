using Bing.Data.Sql.Builders.Multiple;
using Bing.Dapper.Tests.Infrastructure;
using global::Dapper;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证多结果集读取复用 Dapper 缓冲区。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SqliteBufferedGridResultTest
{
    private const int RowCount = 8192;
    private const long NextResultValue = 92741;
    private const string RowsSql = "SELECT id FROM (WITH RECURSIVE sequence(id) AS (VALUES(1) UNION ALL SELECT id + 1 FROM sequence WHERE id < 8192) SELECT id FROM sequence) AS rows ORDER BY id";

    private readonly SqliteIntegrationDatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SqliteBufferedGridResultTest(SqliteIntegrationDatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void Read_ShouldAvoidCopyingTheBufferedRows()
    {
        using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        connection.Open();
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateCommand(executor);

        for (var i = 0; i < 2; i++)
        {
            MeasureNative(connection, command.Sql);
            MeasureFramework(executor, command);
        }

        var nativeBytes = MeasureNative(connection, command.Sql);
        var frameworkBytes = MeasureFramework(executor, command);
        var difference = frameworkBytes - nativeBytes;

        _output.WriteLine($"Dapper GridReader={nativeBytes} B, framework={frameworkBytes} B, delta={difference} B");
        Assert.True(difference < RowCount * sizeof(long) / 2,
            $"Framework Read<long>() allocated {difference} B more than Dapper GridReader.");
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnRowsAndAdvanceToTheNextResult()
    {
        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        using var executor = _fixture.CreateMultipleQueryExecutor();
        var command = CreateCommand(executor);

        using (var native = await connection.QueryMultipleAsync(command.Sql))
        {
            var rows = await native.ReadAsync<long>();
            AssertRows(rows);
            Assert.Equal(NextResultValue, (await native.ReadAsync<long>()).Single());
        }

        using var framework = await executor.ExecuteAsync(command);
        var frameworkRows = await framework.ReadAsync<long>(CancellationToken.None);
        AssertRows(frameworkRows);
        Assert.Equal(NextResultValue,
            (await framework.ReadAsync<long>(CancellationToken.None)).Single());
    }

    private static long MeasureNative(SqliteConnection connection, string sql)
    {
        using var grid = connection.QueryMultiple(sql);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var rows = grid.Read<long>();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        AssertRows(rows);
        Assert.Equal(NextResultValue, grid.Read<long>().Single());
        return allocatedBytes;
    }

    private static long MeasureFramework(ISqlMultipleQueryExecutor executor, SqlMultipleQueryCommand command)
    {
        using var result = executor.Execute(command);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var rows = result.Read<long>();
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        AssertRows(rows);
        Assert.Equal(NextResultValue, result.Read<long>().Single());
        return allocatedBytes;
    }

    private static SqlMultipleQueryCommand CreateCommand(ISqlMultipleQueryExecutor executor) => executor.CreateBatch()
        .Append(RowsSql)
        .Append($"SELECT {NextResultValue}")
        .Build();

    private static void AssertRows(IEnumerable<long> rows) =>
        Assert.Equal(Enumerable.Range(1, RowCount).Select(static id => (long)id), rows);
}
