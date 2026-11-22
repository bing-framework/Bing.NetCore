using Bing.Dapper.Tests.Infrastructure;
using global::Dapper;
using Xunit.Abstractions;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>对照直接调用 Dapper 的分配量，验证列表与分页的缓冲行为。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SqliteBufferedResultTest
{
    private const int RowCount = 8192;
    private const string Source = "(WITH RECURSIVE seq(n) AS (VALUES(1) UNION ALL SELECT n+1 FROM seq WHERE n<8192) SELECT n FROM seq) AS rows";
    private readonly SqliteIntegrationDatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SqliteBufferedResultTest(SqliteIntegrationDatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void ToList_MultiMap_ShouldReuseDapperResultBuffer()
    {
        const string sql = "SELECT n AS A, n AS Id FROM " + Source + " ORDER BY n";
        Func<long, long, long> map = static (left, right) => left + right;
        using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        connection.Open();
        using var root = _fixture.CreateQuery();
        var query = root.Sql(sql);
        AssertBufferAllocation(() => connection.Query(sql, map).AsList(), () => query.ToList(map));
    }

    [Fact]
    public void ToList_Scalar_ShouldReuseDapperResultBuffer()
    {
        const string sql = "SELECT n FROM " + Source + " ORDER BY n";
        using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        connection.Open();
        using var root = _fixture.CreateQuery();
        var query = root.Sql(sql);
        AssertBufferAllocation(() => connection.Query<long>(sql).AsList(), () => query.ToList<long>());
    }

    [Fact]
    public void ToPage_KnownTotal_ShouldAllocateOnlyThePageSnapshotAfterBuffering()
    {
        const string sql = "SELECT n FROM " + Source;
        var pager = new Pager(1, RowCount, RowCount, "n");
        using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        connection.Open();
        using var root = _fixture.CreateQuery();
        var query = root.Sql(sql);
        AssertBufferAllocation(
            () => new PagerList<long>(pager, connection.Query<long>(sql + " ORDER BY n LIMIT 8192 OFFSET 0")).Data,
            () => query.ToPage<long>(pager).Data);
    }

    private void AssertBufferAllocation(Func<List<long>> direct, Func<List<long>> wrapped)
    {
        direct();
        wrapped();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var expected = direct();
        var directBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var actual = wrapped();
        var queryBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(RowCount, actual.Count);
        Assert.Equal(expected, actual);
        _output.WriteLine($"Dapper={directBytes} B, SqlQuery={queryBytes} B, delta={queryBytes - directBytes} B");
        // 允许封装层的固定开销，同时能检出整份 long[] 的复制。
        Assert.True(queryBytes - directBytes < RowCount * sizeof(long) / 2, $"额外分配 {queryBytes - directBytes} B");
    }

    [Fact]
    public async Task ToListAsync_RepeatedMultiMap_ShouldReturnIndependentLists()
    {
        using var root = _fixture.CreateQuery();
        var query = root.Sql("SELECT 1 AS A, 10 AS Id UNION ALL SELECT 2, 20 ORDER BY A");
        Func<long, long, long> map = static (left, right) => left + right;
        var first = await query.ToListAsync(map);
        var second = await query.ToListAsync(map);

        first.Clear();
        Assert.Equal(new long[] { 11, 22 }, second);
    }
}
